// ZXMAK2 native bridge for Aaron Giles' BSD-licensed ymfm YMF278B core.
// The upstream sources are vendored beside this file without functional
// changes.  This wrapper only supplies memory, timer and C ABI glue.

#include <algorithm>
#include <cstdint>
#include <cstring>
#include <new>
#include <vector>

#include "ymfm_opl.h"

#if defined(_WIN32)
#define YMFM_EXPORT extern "C" __declspec(dllexport)
#else
#define YMFM_EXPORT extern "C"
#endif

namespace
{
constexpr uint64_t CLOCKS_PER_SAMPLE = 768;

class opl4_bridge final : public ymfm::ymfm_interface
{
public:
    opl4_bridge(const uint8_t *rom, uint32_t rom_size, uint32_t ram_size) :
        m_rom(rom, rom + rom_size),
        m_ram(ram_size, 0),
        m_chip(*this)
    {
        reset();
    }

    void reset()
    {
        m_clock = 0;
        m_busy_end = 0;
        m_irq = false;
        for (auto &timer : m_timer)
        {
            timer.end = 0;
            timer.active = false;
        }
        m_chip.reset();
    }

    uint8_t read(uint32_t offset)
    {
        return m_chip.read(offset);
    }

    void write(uint32_t offset, uint8_t data)
    {
        m_chip.write(offset, data);
    }

    void generate(int16_t *output, uint32_t samples)
    {
        for (uint32_t index = 0; index < samples; ++index)
        {
            const uint64_t target = m_clock + CLOCKS_PER_SAMPLE;
            service_timers(target);
            ymfm::ymf278b::output_data sample;
            m_chip.generate(&sample);
            output[index * 2 + 0] = static_cast<int16_t>(sample.data[4]);
            output[index * 2 + 1] = static_cast<int16_t>(sample.data[5]);
            m_clock = target;
        }
    }

    bool irq() const { return m_irq; }

protected:
    void ymfm_set_timer(uint32_t number, int32_t duration) override
    {
        if (number >= 2)
            return;
        if (duration < 0)
        {
            m_timer[number].active = false;
            return;
        }
        m_timer[number].end = m_clock + static_cast<uint32_t>(duration);
        m_timer[number].active = true;
    }

    void ymfm_set_busy_end(uint32_t clocks) override
    {
        m_busy_end = m_clock + clocks;
    }

    bool ymfm_is_busy() override
    {
        return m_clock < m_busy_end;
    }

    void ymfm_update_irq(bool asserted) override
    {
        m_irq = asserted;
    }

    uint8_t ymfm_external_read(ymfm::access_class type, uint32_t address) override
    {
        if (type != ymfm::ACCESS_PCM)
            return 0xff;
        if (address < m_rom.size())
            return m_rom[address];
        address -= static_cast<uint32_t>(m_rom.size());
        return address < m_ram.size() ? m_ram[address] : 0xff;
    }

    void ymfm_external_write(ymfm::access_class type, uint32_t address,
        uint8_t data) override
    {
        if (type != ymfm::ACCESS_PCM || address < m_rom.size())
            return;
        address -= static_cast<uint32_t>(m_rom.size());
        if (address < m_ram.size())
            m_ram[address] = data;
    }

private:
    struct timer_state
    {
        uint64_t end = 0;
        bool active = false;
    };

    void service_timers(uint64_t target)
    {
        // The engine callback can immediately arm the timer again.  Keep
        // servicing it until its next edge falls outside this sample period.
        for (uint32_t number = 0; number < 2; ++number)
        {
            int guard = 8;
            while (m_timer[number].active && m_timer[number].end <= target &&
                guard-- > 0)
            {
                m_clock = m_timer[number].end;
                m_timer[number].active = false;
                m_engine->engine_timer_expired(number);
            }
        }
    }

    std::vector<uint8_t> m_rom;
    std::vector<uint8_t> m_ram;
    ymfm::ymf278b m_chip;
    timer_state m_timer[2];
    uint64_t m_clock = 0;
    uint64_t m_busy_end = 0;
    bool m_irq = false;
};
}

YMFM_EXPORT void *ymfm_opl4_create(const uint8_t *rom, uint32_t rom_size,
    uint32_t ram_size)
{
    if (rom == nullptr || rom_size == 0)
        return nullptr;
    try
    {
        return new opl4_bridge(rom, rom_size, ram_size);
    }
    catch (...)
    {
        return nullptr;
    }
}

YMFM_EXPORT void ymfm_opl4_destroy(void *instance)
{
    delete static_cast<opl4_bridge *>(instance);
}

YMFM_EXPORT void ymfm_opl4_reset(void *instance)
{
    if (instance != nullptr)
        static_cast<opl4_bridge *>(instance)->reset();
}

YMFM_EXPORT uint8_t ymfm_opl4_read(void *instance, uint32_t offset)
{
    return instance == nullptr ? 0xff :
        static_cast<opl4_bridge *>(instance)->read(offset);
}

YMFM_EXPORT void ymfm_opl4_write(void *instance, uint32_t offset, uint8_t data)
{
    if (instance != nullptr)
        static_cast<opl4_bridge *>(instance)->write(offset, data);
}

YMFM_EXPORT void ymfm_opl4_generate(void *instance, int16_t *output,
    uint32_t samples)
{
    if (instance != nullptr && output != nullptr)
        static_cast<opl4_bridge *>(instance)->generate(output, samples);
}

YMFM_EXPORT uint8_t ymfm_opl4_irq(void *instance)
{
    return instance != nullptr && static_cast<opl4_bridge *>(instance)->irq();
}
