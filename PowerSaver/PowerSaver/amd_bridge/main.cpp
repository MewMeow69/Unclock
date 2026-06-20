#include <cstdio>
#include <cstring>

#include "ADLXHelper.h"
#include "IGPUManualGFXTuning.h"
#include "IGPUTuning.h"

using namespace adlx;

#define CHECK(x) do { auto r = (x); if (ADLX_FAILED(r)) { std::fprintf(stderr, "FAIL: %s\n  error=0x%X\n", #x, r); return 1; } } while(0)

// all GPUs from ADLX are AMD — no vendor check needed

static int CmdInfo()
{
    ADLXHelper adlx;
    CHECK(adlx.Initialize());
    IADLXSystem* sys = adlx.GetSystemServices();
    IADLXGPUList* gpus = nullptr;
    sys->GetGPUs(&gpus);
    if (!gpus) { std::puts("[]"); return 0; }
    std::putchar('[');
    for (adlx_uint i = 0, n = gpus->Size(); i < n; ++i)
    {
        IADLXGPU* gpu = nullptr;
        gpus->At(i, &gpu);
        if (!gpu) continue;
        if (i > 0) std::putchar(',');
        const char* name = nullptr;
        gpu->Name(&name);
        std::printf("{\"name\":\"%s\",\"amd\":true}", name ? name : "?");
        gpu->Release();
    }
    std::puts("]");
    gpus->Release();
    adlx.Terminate();
    return 0;
}

static int CmdPowerSave()
{
    ADLXHelper adlx;
    CHECK(adlx.Initialize());
    IADLXSystem* sys = adlx.GetSystemServices();

    IADLXGPU* gpu = nullptr;
    {
        IADLXGPUList* gpus = nullptr;
        sys->GetGPUs(&gpus);
        if (gpus->Size() > 0)
        {
            gpus->At(0, &gpu);
            if (gpu) gpu->Acquire();
        }
        gpus->Release();
    }
    if (!gpu) { std::fputs("FAIL: no GPU found\n", stderr); return 1; }

    IADLXGPUTuningServices* tuning = nullptr;
    CHECK(sys->GetGPUTuningServices(&tuning));

    IADLXManualGraphicsTuning2* mt2 = nullptr;
    CHECK(tuning->GetManualGraphicsTuning(gpu, &mt2));

    adlx_int freqMin, freqMax;
    mt2->GetGPUMinFrequency(&freqMin);
    mt2->GetGPUMaxFrequency(&freqMax);
    adlx_int vramMin, vramMax;
    mt2->GetVRAMMinFrequency(&vramMin);
    mt2->GetVRAMMaxFrequency(&vramMax);

    CHECK(mt2->SetGPUMinFrequency(freqMin));
    CHECK(mt2->SetGPUMaxFrequency(freqMin));
    CHECK(mt2->SetVRAMFrequency(vramMin));

    adlx_int powerMin = 0, powerMax = 100;
    if (ADLX_SUCCEEDED(mt2->GetPowerLimitRange(&powerMin, &powerMax)))
        CHECK(mt2->SetPowerLimit(powerMin));

    std::printf("OK: GPU min=%d VRAM min=%d power=%d%%\n", (int)freqMin, (int)vramMin, (int)powerMin);

    mt2->Release();
    tuning->Release();
    gpu->Release();
    adlx.Terminate();
    return 0;
}

static int CmdMediumSave()
{
    ADLXHelper adlx;
    CHECK(adlx.Initialize());
    IADLXSystem* sys = adlx.GetSystemServices();

    IADLXGPU* gpu = nullptr;
    {
        IADLXGPUList* gpus = nullptr;
        sys->GetGPUs(&gpus);
        if (gpus->Size() > 0)
        {
            gpus->At(0, &gpu);
            if (gpu) gpu->Acquire();
        }
        gpus->Release();
    }
    if (!gpu) { std::fputs("FAIL: no GPU found\n", stderr); return 1; }

    IADLXGPUTuningServices* tuning = nullptr;
    CHECK(sys->GetGPUTuningServices(&tuning));

    IADLXManualGraphicsTuning2* mt2 = nullptr;
    CHECK(tuning->GetManualGraphicsTuning(gpu, &mt2));

    adlx_int freqMin, freqMax;
    mt2->GetGPUMinFrequency(&freqMin);
    mt2->GetGPUMaxFrequency(&freqMax);
    adlx_int vramMin, vramMax;
    mt2->GetVRAMMinFrequency(&vramMin);
    mt2->GetVRAMMaxFrequency(&vramMax);

    adlx_int fMid = freqMin + (freqMax - freqMin) / 2;
    adlx_int vMid = vramMin + (vramMax - vramMin) / 2;

    CHECK(mt2->SetGPUMinFrequency(freqMin));
    CHECK(mt2->SetGPUMaxFrequency(fMid));
    CHECK(mt2->SetVRAMFrequency(vMid));

    adlx_int powerMin = 0, powerMax = 100;
    if (ADLX_SUCCEEDED(mt2->GetPowerLimitRange(&powerMin, &powerMax)))
        CHECK(mt2->SetPowerLimit(powerMin + (powerMax - powerMin) / 2));

    std::printf("OK: GPU max=%d VRAM=%d\n", (int)fMid, (int)vMid);

    mt2->Release();
    tuning->Release();
    gpu->Release();
    adlx.Terminate();
    return 0;
}

static int CmdReset()
{
    ADLXHelper adlx;
    CHECK(adlx.Initialize());
    IADLXSystem* sys = adlx.GetSystemServices();

    IADLXGPU* gpu = nullptr;
    {
        IADLXGPUList* gpus = nullptr;
        sys->GetGPUs(&gpus);
        if (gpus->Size() > 0)
        {
            gpus->At(0, &gpu);
            if (gpu) gpu->Acquire();
        }
        gpus->Release();
    }
    if (!gpu) { std::fputs("FAIL: no GPU found\n", stderr); return 1; }

    IADLXGPUTuningServices* tuning = nullptr;
    CHECK(sys->GetGPUTuningServices(&tuning));

    CHECK(tuning->ResetToFactory(gpu));

    std::puts("OK: GPU reset to factory defaults");

    tuning->Release();
    gpu->Release();
    adlx.Terminate();
    return 0;
}

static int CmdDetect()
{
    ADLXHelper adlx;
    CHECK(adlx.Initialize());
    IADLXSystem* sys = adlx.GetSystemServices();

    IADLXGPU* gpu = nullptr;
    {
        IADLXGPUList* gpus = nullptr;
        sys->GetGPUs(&gpus);
        if (gpus->Size() > 0)
        {
            gpus->At(0, &gpu);
            if (gpu) gpu->Acquire();
        }
        gpus->Release();
    }
    if (!gpu) { std::fputs("FAIL: no GPU found\n", stderr); return 1; }

    IADLXGPUTuningServices* tuning = nullptr;
    CHECK(sys->GetGPUTuningServices(&tuning));

    IADLXManualGraphicsTuning2* mt2 = nullptr;
    if (ADLX_FAILED(tuning->GetManualGraphicsTuning(gpu, &mt2)))
    {
        std::puts("{\"mode\":\"auto\"}");
        tuning->Release();
        gpu->Release();
        adlx.Terminate();
        return 0;
    }

    adlx_int hwMin, hwMax, curMin, curMax;
    mt2->GetGPUMinFrequency(&hwMin);
    mt2->GetGPUMaxFrequency(&hwMax);
    mt2->GetGPUMinFrequency(&curMin);
    mt2->GetGPUMaxFrequency(&curMax);

    adlx_int vramMin, vramMax, vramCur;
    mt2->GetVRAMMinFrequency(&vramMin);
    mt2->GetVRAMMaxFrequency(&vramMax);
    mt2->GetVRAMFrequency(&vramCur);

    std::printf("{\"hwMin\":%d,\"hwMax\":%d,\"curMin\":%d,\"curMax\":%d,\"vramCur\":%d,\"vramMin\":%d,\"vramMax\":%d}\n",
        (int)hwMin, (int)hwMax, (int)curMin, (int)curMax, (int)vramCur, (int)vramMin, (int)vramMax);

    mt2->Release();
    tuning->Release();
    gpu->Release();
    adlx.Terminate();
    return 0;
}

int main(int argc, char* argv[])
{
    if (argc < 2)
    {
        std::fputs("Usage: amd_bridge --info|--power-save|--medium-save|--reset|--detect\n", stderr);
        return 1;
    }
    if (!std::strcmp(argv[1], "--info")) return CmdInfo();
    if (!std::strcmp(argv[1], "--power-save")) return CmdPowerSave();
    if (!std::strcmp(argv[1], "--medium-save")) return CmdMediumSave();
    if (!std::strcmp(argv[1], "--reset")) return CmdReset();
    if (!std::strcmp(argv[1], "--detect")) return CmdDetect();
    std::fprintf(stderr, "unknown command: %s\n", argv[1]);
    return 1;
}
