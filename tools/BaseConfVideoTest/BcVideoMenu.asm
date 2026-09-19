; ZX-Evo BaseConf video conformance disk, B23 pattern suite.
; Every slot prepares the physical pages required by its real r1364 format,
; selects the corresponding RG/RGEX route, waits for the renderer
; frame-boundary handoff, and shows a labelled high-contrast pattern.

        DEVICE ZXSPECTRUM128

MENU_ADDRESS    EQU $8000

; Tokenized BASIC. A boot.B program resumes in BASIC ROM, so the disk LOAD
; must explicitly enter TR-DOS through its standard USR 15619 entry point.
; 10 CLEAR VAL "32767": RANDOMIZE USR VAL "15619": REM: LOAD "BCMENU" CODE
; 20 RANDOMIZE USR VAL "32768"
        ORG 23755
basic_loader:
        DB 0,10
        DW basic_line_10_end-basic_line_10
basic_line_10:
        DB $FD,$B0,'"',"32767",'"',':'
        DB $F9,$C0,$B0,'"',"15619",'"',':',$EA,':'
        DB $EF,'"',"BCMENU",'"',$AF,13
basic_line_10_end:
        DB 0,20
        DW basic_line_20_end-basic_line_20
basic_line_20:
        DB $F9,$C0,$B0,'"',"32768",'"',13
basic_line_20_end:
basic_loader_end:

        ORG MENU_ADDRESS
menu_start:
        DI
        LD SP,$BFF0
        XOR A
        LD (selected),A
        CALL save_c000_mapping
        CALL load_text_font
        CALL restore_c000_mapping
        CALL clear_screen
        CALL draw_menu

menu_loop:
        CALL read_command
        CP 1
        JR Z,move_up
        CP 2
        JR Z,move_down
        CP 3
        JR Z,open_selected
        CP 10
        JR C,menu_loop
        SUB 10
        LD (selected),A
        CALL draw_cursor
        JP open_selected

move_up:
        LD A,(selected)
        OR A
        JR NZ,move_up_now
        LD A,7
move_up_now:
        DEC A
        LD (selected),A
        CALL draw_cursor
        JP menu_loop

move_down:
        LD A,(selected)
        INC A
        CP 7
        JR C,move_down_now
        XOR A
move_down_now:
        LD (selected),A
        CALL draw_cursor
        JR menu_loop

open_selected:
        CALL prepare_selected
        CALL select_mode
        CALL wait_mode_frame
wait_page:
        CALL read_command
        CP 4
        JR NZ,wait_page
        CALL select_standard
        CALL wait_mode_frame
        CALL restore_c000_mapping
        CALL clear_screen
        CALL draw_menu
        JP menu_loop

draw_menu:
        LD D,1
        LD E,4
        LD HL,menu_title
        CALL print_at
        LD D,3
        LD E,2
        LD HL,menu_subtitle
        CALL print_at
        LD HL,menu_items
        LD D,6
        LD B,7
draw_items:
        PUSH BC
        PUSH DE
        LD E,3
        CALL print_at
        POP DE
        POP BC
        INC D
        DJNZ draw_items
        LD D,16
        LD E,4
        LD HL,menu_help1
        CALL print_at
        LD D,18
        LD E,3
        LD HL,menu_help2
        CALL print_at
        JP draw_cursor

draw_cursor:
        LD D,6
        LD B,7
erase_cursor:
        PUSH BC
        PUSH DE
        LD E,1
        LD HL,blank_marker
        CALL print_at
        POP DE
        POP BC
        INC D
        DJNZ erase_cursor
        LD A,(selected)
        ADD A,6
        LD D,A
        LD E,1
        LD HL,active_marker
        JP print_at

; Returns: 1=up, 2=down, 3=open, 4=back, 10..16=direct item.
read_command:
        CALL wait_release
read_command_loop:
        LD BC,$FBFE
        IN A,(C)
        BIT 0,A
        JR Z,key_up
        LD BC,$FDFE
        IN A,(C)
        BIT 0,A
        JR Z,key_down
        LD BC,$BFFE
        IN A,(C)
        BIT 0,A
        JR Z,key_open
        LD BC,$7FFE
        IN A,(C)
        BIT 0,A
        JR Z,key_back
        LD BC,$F7FE
        IN A,(C)
        BIT 0,A
        JR Z,key_1
        BIT 1,A
        JR Z,key_2
        BIT 2,A
        JR Z,key_3
        BIT 3,A
        JR Z,key_4
        BIT 4,A
        JR Z,key_5
        LD BC,$EFFE
        IN A,(C)
        BIT 4,A
        JR Z,key_6
        BIT 3,A
        JR Z,key_7
        JR read_command_loop
key_up:    LD A,1 : RET
key_down:  LD A,2 : RET
key_open:  LD A,3 : RET
key_back:  LD A,4 : RET
key_1:     LD A,10 : RET
key_2:     LD A,11 : RET
key_3:     LD A,12 : RET
key_4:     LD A,13 : RET
key_5:     LD A,14 : RET
key_6:     LD A,15 : RET
key_7:     LD A,16 : RET

wait_release:
        LD BC,$FEFE : IN A,(C) : AND $1F : CP $1F : JR NZ,wait_release
        LD B,$FD    : IN A,(C) : AND $1F : CP $1F : JR NZ,wait_release
        LD B,$FB    : IN A,(C) : AND $1F : CP $1F : JR NZ,wait_release
        LD B,$F7    : IN A,(C) : AND $1F : CP $1F : JR NZ,wait_release
        LD B,$EF    : IN A,(C) : AND $1F : CP $1F : JR NZ,wait_release
        LD B,$DF    : IN A,(C) : AND $1F : CP $1F : JR NZ,wait_release
        LD B,$BF    : IN A,(C) : AND $1F : CP $1F : JR NZ,wait_release
        LD B,$7F    : IN A,(C) : AND $1F : CP $1F : JR NZ,wait_release
        RET

clear_screen:
        XOR A
        LD HL,$4000
        LD DE,$4001
        LD BC,6143
        LD (HL),A
        LDIR
        LD A,$07
        LD HL,$5800
        LD DE,$5801
        LD BC,767
        LD (HL),A
        LDIR
        RET

; Prepare the memory layout required by the selected renderer.
prepare_selected:
        LD A,(selected)
        OR A
        JP Z,prepare_zx
        DEC A
        JP Z,prepare_hwm
        DEC A
        JP Z,prepare_a16
        DEC A
        JP Z,prepare_atm320
        DEC A
        JP Z,prepare_atm640
        DEC A
        JP Z,prepare_atm_text
        JP prepare_evo_text

prepare_zx:
        CALL clear_screen
        CALL draw_pattern
        JP draw_standard_labels

prepare_hwm:
        LD A,5
        CALL map_page_c000
        LD HL,$C000
        LD DE,$C001
        LD BC,$1FFF
        LD A,$AA
        LD (HL),A
        LDIR
        LD HL,$E000
        LD DE,$E001
        LD BC,$1FFF
        LD A,$47
        LD (HL),A
        LDIR
        JP draw_standard_labels

prepare_a16:
        LD A,4
        LD E,$47
        CALL fill_page
        LD A,5
        LD E,$B8
        CALL fill_page
        XOR A
        LD HL,title_a16_r1
        LD D,4
        LD E,3
        CALL draw_pair_text
        XOR A
        LD HL,pattern_line
        LD D,10
        LD E,3
        CALL draw_pair_text
        XOR A
        LD HL,space_line
        LD D,18
        LD E,3
        JP draw_pair_text

prepare_atm320:
        LD A,1
        LD E,$47
        CALL fill_page
        LD A,5
        LD E,$B8
        CALL fill_page
        LD A,1
        LD HL,title_320_r1
        LD D,4
        LD E,6
        CALL draw_pair_text
        LD A,1
        LD HL,pattern_line
        LD D,11
        LD E,7
        CALL draw_pair_text
        LD A,1
        LD HL,space_line
        LD D,20
        LD E,7
        JP draw_pair_text

prepare_atm640:
        LD A,1
        LD E,$47
        CALL fill_page
        LD A,5
        LD E,$AA
        CALL fill_page
        LD HL,title_640_r1
        LD D,4
        LD E,24
        CALL draw_640_text
        LD HL,pattern_line
        LD D,11
        LD E,25
        CALL draw_640_text
        LD HL,space_line
        LD D,20
        LD E,26
        JP draw_640_text

prepare_atm_text:
        LD A,5
        LD E,0
        CALL fill_page
        LD A,1
        LD E,$47
        CALL fill_page
        XOR A
        JP draw_text_page

prepare_evo_text:
        LD A,8
        LD E,0
        CALL fill_page
        LD A,8
        CALL map_page_c000
        LD HL,$E1C0
        LD DE,$E1C1
        LD BC,$0627
        LD A,$47
        LD (HL),A
        LDIR
        LD HL,$F1C0
        LD DE,$F1C1
        LD BC,$0627
        LD A,$47
        LD (HL),A
        LDIR
        LD A,1
        JP draw_text_page

draw_standard_labels:
        LD D,2
        LD E,4
        LD HL,page_header
        CALL print_at
        LD A,(selected)
        ADD A,A
        LD C,A
        LD B,0
        LD HL,page_titles
        ADD HL,BC
        LD C,(HL)
        INC HL
        LD B,(HL)
        LD H,B
        LD L,C
        LD D,6
        LD E,2
        CALL print_at
        LD D,12
        LD E,2
        LD HL,next_stage
        CALL print_at
        LD D,18
        LD E,3
        LD HL,space_back
        JP print_at

; ZX bitmap pattern used by the standard and HWM-labelled pages.
draw_pattern:
        LD HL,$4000
        LD DE,$4001
        LD BC,6143
        LD A,$AA
        LD (HL),A
        LDIR
        LD HL,$5800
        LD DE,$5801
        LD BC,767
        LD A,$47
        LD (HL),A
        LDIR
        RET

; Preserve the active C000 descriptor selected by the current #7FFD map.
; #00BE..#07BE return inverted physical page numbers; #08BE and #09BE
; return the RAM/ROM and dos7ffd descriptor bits; #0ABE returns #7FFD.
save_c000_mapping:
        LD BC,$00BF
        IN A,(C)
        AND $0F
        LD (saved_bf),A
        LD BC,$0ABE
        IN A,(C)
        LD (saved_7ffd),A
        AND $10
        LD A,3
        JR Z,save_c000_index_ready
        LD A,7
save_c000_index_ready:
        LD (saved_c000_index),A
        LD B,A
        LD C,$BE
        IN A,(C)
        LD (saved_c000_page),A
        LD BC,$08BE
        IN A,(C)
        LD (saved_ramnroms),A
        LD BC,$09BE
        IN A,(C)
        LD (saved_dos7ffds),A
        RET

; Map linear physical RAM page A into C000 through the BaseConf manager.
; Shadow D0 is opened only for the two pager writes and all XXBF bits are
; restored afterwards.  #FFF7 disables dos7ffd substitution for this window;
; #F7F7 then selects the full 0..255 physical page (inverted on the bus).
map_page_c000:
        PUSH AF
        LD BC,$00BF
        IN A,(C)
        AND $0F
        LD (map_saved_bf),A
        OR 1
        OUT (C),A
        LD BC,$FFF7
        LD A,$7F
        OUT (C),A
        POP AF
        CPL
        LD BC,$F7F7
        OUT (C),A
        LD BC,$00BF
        LD A,(map_saved_bf)
        OUT (C),A
        RET

; Restore the exact active C000 descriptor captured at BOOT.  For RAM the
; direct selector restores all eight physical-page bits; for ROM the low six
; descriptor page bits are sufficient, as defined by BaseConf.
restore_c000_mapping:
        LD BC,$00BF
        IN A,(C)
        AND $0F
        LD (map_saved_bf),A
        OR 1
        OUT (C),A

        LD A,(saved_c000_index)
        LD E,1
restore_mask_loop:
        OR A
        JR Z,restore_mask_ready
        SLA E
        DEC A
        JR restore_mask_loop
restore_mask_ready:
        LD A,(saved_c000_page)
        AND $3F
        LD D,A
        LD A,(saved_ramnroms)
        AND E
        JR Z,restore_ram_bit_ready
        SET 6,D
restore_ram_bit_ready:
        LD A,(saved_dos7ffds)
        AND E
        JR Z,restore_dos_bit_ready
        SET 7,D
restore_dos_bit_ready:
        LD BC,$FFF7
        OUT (C),D

        LD A,(saved_ramnroms)
        AND E
        JR Z,restore_c000_close
        LD A,(saved_c000_page)
        LD BC,$F7F7
        OUT (C),A
restore_c000_close:
        LD BC,$00BF
        LD A,(map_saved_bf)
        OUT (C),A
        RET

; A=encoded page, E=fill byte.
fill_page:
        CALL map_page_c000
        LD A,E
        LD HL,$C000
        LD DE,$C001
        LD BC,$3FFF
        LD (HL),A
        LDIR
        RET

; Load the Spectrum ROM font (characters 32..127) into the BaseConf 2 KiB
; character generator.  Page 0 is used as a scratch write target, XXBF.D2
; enables font writes, and all pre-existing XXBF bits are retained.
load_text_font:
        XOR A
        CALL map_page_c000
        LD BC,$00BF
        IN A,(C)
        AND $0F
        LD (font_saved_bf),A
        OR 4
        OUT (C),A
        LD HL,$3D00
        LD DE,$C100
        LD BC,768
        LDIR
        LD BC,$00BF
        LD A,(font_saved_bf)
        OUT (C),A
        RET

; Draw a string in either packed 16-colour format.
; A=0: Pentagon 256x192, pages 4/5, ZX address permutation.
; A=1: ATM 320x200, pages 1/5, linear 40-byte pair stride.
; HL=string, D=character row, E=character column.
draw_pair_text:
        LD (pair_layout),A
        LD (pair_string),HL
        LD A,D
        LD (pair_row),A
        LD A,E
        LD (pair_col),A
draw_pair_next:
        LD HL,(pair_string)
        LD A,(HL)
        INC HL
        LD (pair_string),HL
        OR A
        RET Z
        SUB 32
        LD L,A
        LD H,0
        ADD HL,HL
        ADD HL,HL
        ADD HL,HL
        LD DE,$3D00
        ADD HL,DE
        LD (pair_glyph),HL
        XOR A
        LD (pair_scan),A
draw_pair_scan:
        LD HL,(pair_glyph)
        LD A,(HL)
        LD (pair_bits),A
        CALL calc_pair_dest
        LD (pair_dest),HL
        XOR A
        LD (pair_index),A
draw_pair_quartet:
        LD A,(pair_bits)
        AND $C0
        RLCA
        RLCA
        LD E,A
        LD D,0
        LD HL,pair_values
        ADD HL,DE
        LD A,(HL)
        LD (pair_value),A

        LD A,(pair_index)
        AND 1
        JR NZ,draw_pair_page_b
        LD A,(pair_layout)
        OR A
        LD A,4
        JR Z,draw_pair_page_ready
        LD A,1
        JR draw_pair_page_ready
draw_pair_page_b:
        LD A,5
draw_pair_page_ready:
        CALL map_page_c000
        LD HL,(pair_dest)
        LD A,(pair_index)
        BIT 1,A
        JR Z,draw_pair_half_ready
        LD DE,$2000
        ADD HL,DE
draw_pair_half_ready:
        LD A,(pair_value)
        LD (HL),A
        LD A,(pair_bits)
        ADD A,A
        ADD A,A
        LD (pair_bits),A
        LD A,(pair_index)
        INC A
        LD (pair_index),A
        CP 4
        JR NZ,draw_pair_quartet

        LD HL,(pair_glyph)
        INC HL
        LD (pair_glyph),HL
        LD A,(pair_scan)
        INC A
        LD (pair_scan),A
        CP 8
        JR NZ,draw_pair_scan
        LD A,(pair_col)
        INC A
        LD (pair_col),A
        JP draw_pair_next

calc_pair_dest:
        LD A,(pair_layout)
        OR A
        JR NZ,calc_pair_atm320
        LD A,(pair_row)
        ADD A,A
        LD E,A
        LD D,0
        LD HL,screen_rows
        ADD HL,DE
        LD E,(HL)
        INC HL
        LD D,(HL)
        EX DE,HL
        LD A,(pair_col)
        LD E,A
        LD D,0
        ADD HL,DE
        LD A,(pair_scan)
        ADD A,H
        LD H,A
        SET 7,H
        RET
calc_pair_atm320:
        LD A,(pair_row)
        ADD A,A
        ADD A,A
        ADD A,A
        LD E,A
        LD A,(pair_scan)
        ADD A,E
        LD L,A
        LD H,0
        ADD HL,HL
        ADD HL,HL
        ADD HL,HL
        LD D,H
        LD E,L
        ADD HL,HL
        ADD HL,HL
        ADD HL,DE
        LD A,(pair_col)
        LD E,A
        LD D,0
        ADD HL,DE
        LD DE,$C000
        ADD HL,DE
        RET

; Draw 8-pixel ROM-font characters in ATM 640x200 HWM bitmap page 5.
; HL=string, D=character row, E=character column.
draw_640_text:
        LD (wide_string),HL
        LD A,D
        LD (wide_row),A
        LD A,E
        LD (wide_col),A
        LD A,5
        CALL map_page_c000
draw_640_next:
        LD HL,(wide_string)
        LD A,(HL)
        INC HL
        LD (wide_string),HL
        OR A
        RET Z
        SUB 32
        LD L,A
        LD H,0
        ADD HL,HL
        ADD HL,HL
        ADD HL,HL
        LD DE,$3D00
        ADD HL,DE
        LD (wide_glyph),HL
        XOR A
        LD (wide_scan),A
draw_640_scan:
        CALL calc_640_dest
        LD DE,(wide_glyph)
        LD A,(DE)
        LD (HL),A
        INC DE
        LD (wide_glyph),DE
        LD A,(wide_scan)
        INC A
        LD (wide_scan),A
        CP 8
        JR NZ,draw_640_scan
        LD A,(wide_col)
        INC A
        LD (wide_col),A
        JR draw_640_next

calc_640_dest:
        LD A,(wide_row)
        ADD A,A
        ADD A,A
        ADD A,A
        LD E,A
        LD A,(wide_scan)
        ADD A,E
        LD L,A
        LD H,0
        ADD HL,HL
        ADD HL,HL
        ADD HL,HL
        ADD HL,HL
        LD D,H
        LD E,L
        ADD HL,HL
        ADD HL,HL
        ADD HL,DE
        LD A,(wide_col)
        LD E,A
        LD D,0
        ADD HL,DE
        SRL H
        RR L
        JR NC,calc_640_lower
        LD DE,$2000
        ADD HL,DE
calc_640_lower:
        LD DE,$C000
        ADD HL,DE
        RET

; Draw labels in the two 80x25 text layouts.  A=0 ATM split pages 5/1,
; A=1 BaseConf one-page text in physical page 8.
draw_text_page:
        LD (text_layout),A
        OR A
        LD A,5
        JR Z,draw_text_page_map
        LD A,8
draw_text_page_map:
        CALL map_page_c000
        LD HL,text_title_atm
        LD A,(text_layout)
        OR A
        JR Z,draw_text_title_ready
        LD HL,text_title_evo
draw_text_title_ready:
        LD D,4
        LD E,25
        CALL draw_text_line
        LD HL,text_pattern
        LD D,11
        LD E,24
        CALL draw_text_line
        LD HL,text_space
        LD D,20
        LD E,25
        JP draw_text_line

draw_text_line:
        LD (text_string),HL
        LD A,D
        LD (text_row),A
        LD A,E
        LD (text_col),A
draw_text_next:
        LD HL,(text_string)
        LD A,(HL)
        INC HL
        LD (text_string),HL
        OR A
        RET Z
        LD (text_char),A
        CALL calc_text_dest
        LD A,(text_char)
        LD (HL),A
        LD A,(text_col)
        INC A
        LD (text_col),A
        JR draw_text_next

calc_text_dest:
        LD A,(text_row)
        LD L,A
        LD H,0
        ADD HL,HL
        ADD HL,HL
        ADD HL,HL
        ADD HL,HL
        ADD HL,HL
        ADD HL,HL
        LD A,(text_col)
        LD E,A
        SRL E
        LD D,0
        ADD HL,DE
        LD A,(text_col)
        AND 1
        LD DE,$01C0
        JR Z,calc_text_lane_ready
        LD A,(text_layout)
        OR A
        LD DE,$21C0
        JR Z,calc_text_lane_ready
        LD DE,$11C0
calc_text_lane_ready:
        ADD HL,DE
        LD DE,$C000
        ADD HL,DE
        RET

; raw selectors: ZX=3, Pentagon HWM=19, Pentagon 16c=11,
; ATM 320=0, ATM 640=2, ATM text=6, BaseConf text=7.
select_mode:
        LD A,(selected)
        LD E,A
        LD D,0
        LD HL,mode_selectors
        ADD HL,DE
        LD A,(HL)
        LD (mode_raw),A

        ; EFF7.RGEX: code 1 is bit 0, code 2 is bit 5.
        LD C,A
        SRL A
        SRL A
        SRL A
        CP 1
        JR Z,mode_rgex_1
        CP 2
        JR Z,mode_rgex_2
        XOR A
        JR mode_write_rgex
mode_rgex_1:
        LD A,1
        JR mode_write_rgex
mode_rgex_2:
        LD A,$20
mode_write_rgex:
        LD B,$EF
        LD C,$F7
        OUT (C),A

        ; XXBF.D0 opens the system shadow gate for the FF77 RG write.
        LD BC,$00BF
        LD A,1
        OUT (C),A
        LD A,(mode_raw)
        AND 7
        LD BC,$FF77
        OUT (C),A
        LD BC,$00BF
        XOR A
        OUT (C),A
        RET

select_standard:
        XOR A
        LD (selected),A
        JP select_mode

wait_mode_frame:
        ; Keep the ROM interrupt handler out of physical page 5.  Its standard
        ; #5Cxx system-variable writes are visible around line 179 in ATM HWM.
        ; This delay crosses many complete frames even in the longest profile,
        ; allowing the emulator's frame-boundary route handoff without EI/HALT.
        DI
        LD BC,0
wait_mode_frame_loop:
        DEC BC
        LD A,B
        OR C
        JR NZ,wait_mode_frame_loop
        RET

; D=row, E=column, HL=zero-terminated text.
print_at:
        LD A,D
        LD (cursor_y),A
        LD A,E
        LD (cursor_x),A
print_at_loop:
        LD A,(HL)
        INC HL
        OR A
        RET Z
        PUSH HL
        CALL print_char
        POP HL
        JR print_at_loop

; A=ASCII. The standard Spectrum ROM font is used only by this ZX shell.
print_char:
        SUB 32
        RET C
        PUSH BC
        PUSH DE
        PUSH HL
        LD L,A
        LD H,0
        ADD HL,HL
        ADD HL,HL
        ADD HL,HL
        LD DE,$3D00
        ADD HL,DE
        PUSH HL
        LD A,(cursor_y)
        ADD A,A
        LD E,A
        LD D,0
        LD HL,screen_rows
        ADD HL,DE
        LD C,(HL)
        INC HL
        LD B,(HL)
        LD A,(cursor_x)
        ADD A,C
        LD C,A
        POP HL
        LD D,8
print_glyph:
        LD A,(HL)
        LD (BC),A
        INC HL
        INC B
        DEC D
        JR NZ,print_glyph
        LD HL,cursor_x
        INC (HL)
        POP HL
        POP DE
        POP BC
        RET

selected:       DB 0
mode_raw:       DB 3
cursor_y:       DB 0
cursor_x:       DB 0
saved_bf:       DB 0
saved_7ffd:     DB 0
saved_c000_index: DB 3
saved_c000_page:  DB $FF
saved_ramnroms: DB 0
saved_dos7ffds: DB 0
map_saved_bf:   DB 0
font_saved_bf:  DB 0

pair_layout:    DB 0
pair_row:       DB 0
pair_col:       DB 0
pair_scan:      DB 0
pair_bits:      DB 0
pair_index:     DB 0
pair_value:     DB 0
pair_string:    DW 0
pair_glyph:     DW 0
pair_dest:      DW 0

wide_row:       DB 0
wide_col:       DB 0
wide_scan:      DB 0
wide_string:    DW 0
wide_glyph:     DW 0

text_layout:    DB 0
text_row:       DB 0
text_col:       DB 0
text_char:      DB 0
text_string:    DW 0

pair_values:
        DB $00,$B8,$47,$FF

mode_selectors:
        DB 3,19,11,0,2,6,7

screen_rows:
        DW $4000,$4020,$4040,$4060,$4080,$40A0,$40C0,$40E0
        DW $4800,$4820,$4840,$4860,$4880,$48A0,$48C0,$48E0
        DW $5000,$5020,$5040,$5060,$5080,$50A0,$50C0,$50E0

menu_title:     DB "BASECONF VIDEO B23",0
menu_subtitle:  DB "BOOT SHELL / MODE SLOTS",0
menu_items:
        DB "1 ZX 256X192 ATTR",0
        DB "2 PENTAGON 256 HWM",0
        DB "3 PENTAGON 256 16C",0
        DB "4 ATM 320X200 16C",0
        DB "5 ATM 640X200 HWM",0
        DB "6 ATM TEXT 80X25",0
        DB "7 BASECONF TEXT 80X25",0
menu_help1:     DB "Q/A MOVE  ENTER OPEN",0
menu_help2:     DB "1-7 DIRECT  SPACE BACK",0
active_marker:  DB ">",0
blank_marker:   DB " ",0

page_header:    DB "B23 TEST SLOT",0
slot_only:      DB "FORMAT MEMORY MAP ACTIVE",0
next_stage:     DB "LABEL + PATTERN / ROUTE ACTIVE",0
space_back:     DB "PRESS SPACE TO MENU",0
page_titles:
        DW title_zx,title_phm,title_p16,title_a320,title_a640,title_atxt,title_btxt
title_zx:       DB "ZX 256X192 ATTR",0
title_phm:      DB "PENTAGON 256 HWM",0
title_p16:      DB "PENTAGON 256 16C",0
title_a320:     DB "ATM 320X200 16C",0
title_a640:     DB "ATM 640X200 HWM",0
title_atxt:     DB "ATM TEXT 80X25",0
title_btxt:     DB "BASECONF TEXT 80X25",0

title_a16_r1:   DB "B23 PENTAGON 256 16C",0
title_320_r1:   DB "B23 ATM 320X200 16C",0
title_640_r1:   DB "B23 ATM 640X200 HWM",0
pattern_line:   DB "FORMAT PATTERN ACTIVE",0
space_line:     DB "PRESS SPACE TO MENU",0

text_title_atm: DB "B23 ATM TEXT 80X25",0
text_title_evo: DB "B23 BASECONF TEXT 80X25",0
text_pattern:   DB "CHARACTER GENERATOR + ATTRIBUTES ACTIVE",0
text_space:     DB "PRESS SPACE TO MENU",0

menu_end:

        EMPTYTRD "build/BCVIDTEST.TRD","BC23"
        SAVETRD "build/BCVIDTEST.TRD","boot.B",basic_loader,basic_loader_end-basic_loader,10
        SAVETRD "build/BCVIDTEST.TRD","BCMENU.C",menu_start,menu_end-menu_start
