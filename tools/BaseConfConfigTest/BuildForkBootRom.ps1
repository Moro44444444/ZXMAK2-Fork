param(
    [Parameter(Mandatory=$true)][string]$InputRom,
    [Parameter(Mandatory=$true)][string]$OutputRom,
    [Parameter(Mandatory=$true)][string]$Mhmt,
    [switch]$Analyze
)
$ErrorActionPreference='Stop'
# Cosmetic fork of the accepted official FE ROM, not a new BIOS revision.
# Sources: NedoPC rom/page5/source/mainmenu.a80 and mainmenu/src/main.a80,
# mirrored at https://github.com/aaydev/zxevo.pentevo (branch main).
if ((Get-FileHash -LiteralPath $InputRom).Hash -ne '620146534DF8A49C6B9042DF45812D1E7F90683DD8F7B813CA2C5ECAC96DC1CA') {
    throw 'Unsupported ROM: only the accepted v0.61.01 FE image may be patched.'
}
$rom=[IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $InputRom))
$bank=26*0x4000
$start=0x10D
if([Text.Encoding]::ASCII.GetString($rom,$bank+0x3FF8,6) -ne 'MNMENU') { throw 'Main menu signature missing' }
$length=0x3FF6-[BitConverter]::ToUInt16($rom,$bank+0x3FF6)-$start
$packed=New-Object byte[] $length
[Array]::Copy($rom,$bank+$start,$packed,0,$length)
$scratch=Join-Path ([IO.Path]::GetTempPath()) ('zxmak2-boot-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $scratch | Out-Null
try {
    $packedPath=Join-Path $scratch 'menu.mlz'
    $plainPath=Join-Path $scratch 'menu.bin'
    $newPackedPath=Join-Path $scratch 'fork.mlz'
    [IO.File]::WriteAllBytes($packedPath,$packed)
    & $Mhmt -mlz -d $packedPath $plainPath
    if($LASTEXITCODE -ne 0){throw 'Menu decompression failed'}
    $plain=[IO.File]::ReadAllBytes($plainPath)
    function Find-Unique([byte[]]$data,[byte[]]$pattern) {
        $found=-1
        for($i=0;$i -le $data.Length-$pattern.Length;$i++) {
            if($data[$i] -ne $pattern[0]){continue}
            $match=$true
            for($j=1;$j -lt $pattern.Length;$j++){if($data[$i+$j] -ne $pattern[$j]){$match=$false;break}}
            if($match){if($found -ge 0){throw 'Ambiguous ROM signature'};$found=$i}
        }
        if($found -lt 0){throw 'ROM signature not found'}
        return $found
    }
    $confLabel=Find-Unique $plain ([Text.Encoding]::ASCII.GetBytes('Baseconf: '))
    $bootLabel=Find-Unique $plain ([Text.Encoding]::ASCII.GetBytes('AVR Boot: '))
    $conf=$confLabel+12 # label followed by two ink-control bytes
    $boot=$bootLabel+12
    $web=Find-Unique $plain ([Text.Encoding]::ASCII.GetBytes('www.nedopc.com'))
    $confAddress=$conf+0x6000
    $bootAddress=$boot+0x6000
    # LD DE,VERS_CONF; LD L,0; CALL GET_VERS_EVO;
    # LD DE,VERS_BOOT; LD L,1; JP GET_VERS_EVO.
    $prefix=[byte[]]@(0x11,($confAddress-band 255),($confAddress-shr 8),0x2E,0)
    $call=Find-Unique $plain $prefix
    if($plain[$call+5] -ne 0xCD -or $plain[$call+8] -ne 0x11 -or
       [BitConverter]::ToUInt16($plain,$call+9) -ne $bootAddress -or
       $plain[$call+11] -ne 0x2E -or $plain[$call+12] -ne 1 -or
       $plain[$call+13] -ne 0xC3 -or
       [BitConverter]::ToUInt16($plain,$call+6) -ne [BitConverter]::ToUInt16($plain,$call+14)) {
        throw 'Version formatter call sequence differs from the documented menu'
    }
    Write-Output ('Menu bytes={0}, version buffers=#{1:X4}/#{2:X4}, formatter=#{3:X4}' -f $plain.Length,$confAddress,$bootAddress,($call+0x6000))
    if($Analyze){return}
    foreach($slot in @(@($conf,'BaseConf Emu (ZXMAK2-Fork)'),@($boot,'Boot Emu (ZXMAK2-Fork)'))) {
        $offset=[int]$slot[0]
        if([Text.Encoding]::ASCII.GetString($plain,$offset,32) -ne 'NONE'.PadRight(32)){throw 'Version buffer differs'}
        [Array]::Copy([Text.Encoding]::ASCII.GetBytes($slot[1].PadRight(32)),0,$plain,$offset,32)
    }
    # Static emulator labels replace only the two calls which fill these slots;
    # date/register protocol, other CMOS users and the actual firmware stay intact.
    $plain[$call+5]=0; $plain[$call+6]=0; $plain[$call+7]=0
    $plain[$call+13]=0xC9; $plain[$call+14]=0; $plain[$call+15]=0
    $plain[$confLabel+4]=[byte][char]'C' # Baseconf -> BaseConf
    if([Text.Encoding]::ASCII.GetString($plain,$web,14) -ne 'www.nedopc.com'){throw 'NedoPC credit changed'}
    [IO.File]::WriteAllBytes($plainPath,$plain)
    & $Mhmt -mlz $plainPath $newPackedPath
    if($LASTEXITCODE -ne 0){throw 'Menu compression failed'}
    $replacement=[IO.File]::ReadAllBytes($newPackedPath)
    if($replacement.Length -gt 0x3FF6-$start){throw 'Compressed menu does not fit'}
    for($i=$start;$i -lt 0x3FF6;$i++){$rom[$bank+$i]=255}
    [Array]::Copy($replacement,0,$rom,$bank+$start,$replacement.Length)
    $free=[BitConverter]::GetBytes([uint16](0x3FF6-$start-$replacement.Length))
    [Array]::Copy($free,0,$rom,$bank+0x3FF6,2)
    $roundTrip=Join-Path $scratch 'verify.bin'
    & $Mhmt -mlz -d $newPackedPath $roundTrip
    if($LASTEXITCODE -ne 0 -or (Get-FileHash $roundTrip).Hash -ne (Get-FileHash $plainPath).Hash){throw 'Compression round-trip failed'}
    [IO.File]::WriteAllBytes([IO.Path]::GetFullPath($OutputRom),$rom)
    Write-Output ('ROM ready; packed bytes={0}; free={1}; SHA256={2}' -f $replacement.Length,([BitConverter]::ToUInt16($rom,$bank+0x3FF6)),(Get-FileHash $OutputRom).Hash)
} finally {
    # Only the newly-created, GUID-named task directory, with a validated parent.
    if([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($scratch)) -eq [IO.Path]::GetTempPath().TrimEnd('\')) {
        Remove-Item -LiteralPath $scratch -Recurse -Force
    }
}
