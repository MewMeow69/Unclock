#include <cstdio>
#include <cstring>
#include <vector>

#include "ADLX/ADLX.hpp"
#include "ADLX/ADLXHelper.h"

#define CHECK(x) do { auto r = (x); if (ADLX_FAILED(r)) { std::fprintf(stderr, "FAIL: %s\n  error=0x%X\n", #x, r); return 1; } } while(0)

static adlx_bool IsAMD(IADLXGPU* gpu)
{
    ADLX_AMD_GPU_TYPE t;
    gpu->GetAMDGPUVariant(&t);
    return t != AMD_GPU_UNKNOWN;
}

static int CmdInfo()
{
    ADLXHelper adlx;
    CHECK(adlx.Initialize());
    IADLXSystem* sys = adlx.GetSystem();
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
        std::printf("{\"name\":\"%s\",\"amd\":%s}", name ? name : "?", IsAMD(gpu) ? "true" : "false");
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
    IADLXSystem* sys = adlx.GetSystem();

    // get the first AMD GPU
    IADLXGPU* gpu = nullptr;
    {
        IADLXGPUList* gpus = nullptr;
        sys->GetGPUs(&gpus);
        for (adlx_uint i = 0, n = gpus->Size(); i < n; ++i)
        {
            IADLXGPU* g = nullptr;
            gpus->At(i, &g);
            if (g && IsAMD(g)) { gpu = g; gpu->AddRef(); g->Release(); break; }
            if (g) g->Release();
        }
        gpus->Release();
    }
    if (!gpu) { std::fputs("FAIL: no AMD GPU found\n", stderr); return 1; }

    IADLXGPUTuningServices* tuning = nullptr;
    CHECK(sys->GetGPUTuningServices(&tuning));

    // manual tuning v2 — most widely supported on modern AMD GPUs
    IADLXManualGraphicsTuning2* mt2 = nullptr;
    CHECK(tuning->GetManualGraphicsTuning(gpu, &mt2));

    adlx_int freqMin, freqMax;
    mt2->GetGPUMinFrequency(&freqMin);
    mt2->GetGPUMaxFrequency(&freqMax);
    adlx_int vramMin, vramMax;
    mt2->GetVRAMMinFrequency(&vramMin);
    mt2->GetVRAMMaxFrequency(&vramMax);

    // clamp to hardware minimum
    adlx_int fmin = freqMin;  // hardware floor (already the min)
    adlx_int vmin = vramMin;

    CHECK(mt2->SetGPUMinFrequency(fmin));
    CHECK(mt2->SetGPUMaxFrequency(fmin));
    CHECK(mt2->SetVRAMFrequency(vmin));

    // query the valid power range and use the minimum
    adlx_int powerMin = 0, powerMax = 100;
    if (ADLX_SUCCEEDED(mt2->GetPowerLimitRange(&powerMin, &powerMax)))
        CHECK(mt2->SetPowerLimit(powerMin));
    // else: power limit is optional — freq lock already saves power

    std::printf("OK: GPU min=%d VRAM min=%d power=%d%%\n", (int)fmin, (int)vmin, (int)powerMin);

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
    IADLXSystem* sys = adlx.GetSystem();

    IADLXGPU* gpu = nullptr;
    {
        IADLXGPUList* gpus = nullptr;
        sys->GetGPUs(&gpus);
        for (adlx_uint i = 0, n = gpus->Size(); i < n; ++i)
        {
            IADLXGPU* g = nullptr;
            gpus->At(i, &g);
            if (g && IsAMD(g)) { gpu = g; gpu->AddRef(); g->Release(); break; }
            if (g) g->Release();
        }
        gpus->Release();
    }
    if (!gpu) { std::fputs("FAIL: no AMD GPU found\n", stderr); return 1; }

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

    // midpoint between min and max
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
    IADLXSystem* sys = adlx.GetSystem();

    IADLXGPU* gpu = nullptr;
    {
        IADLXGPUList* gpus = nullptr;
        sys->GetGPUs(&gpus);
        for (adlx_uint i = 0, n = gpus->Size(); i < n; ++i)
        {
            IADLXGPU* g = nullptr;
            gpus->At(i, &g);
            if (g && IsAMD(g)) { gpu = g; gpu->AddRef(); g->Release(); break; }
            if (g) g->Release();
        }
        gpus->Release();
    }
    if (!gpu) { std::fputs("FAIL: no AMD GPU found\n", stderr); return 1; }

    IADLXGPUTuningServices* tuning = nullptr;
    CHECK(sys->GetGPUTuningServices(&tuning));

    IADLXManualGraphicsTuning2* mt2 = nullptr;
    CHECK(tuning->GetManualGraphicsTuning(gpu, &mt2));

    CHECK(mt2->ResetToFactory());

    std::puts("OK: GPU reset to factory defaults");

    mt2->Release();
    tuning->Release();
    gpu->Release();
    adlx.Terminate();
    return 0;
}

static int CmdDetect()
{
    ADLXHelper adlx;
    CHECK(adlx.Initialize());
    IADLXSystem* sys = adlx.GetSystem();

    IADLXGPU* gpu = nullptr;
    {
        IADLXGPUList* gpus = nullptr;
        sys->GetGPUs(&gpus);
        for (adlx_uint i = 0, n = gpus->Size(); i < n; ++i)
        {
            IADLXGPU* g = nullptr;
            gpus->At(i, &g);
            if (g && IsAMD(g)) { gpu = g; gpu->AddRef(); g->Release(); break; }
            if (g) g->Release();
        }
        gpus->Release();
    }
    if (!gpu) { std::fputs("FAIL: no AMD GPU found\n", stderr); return 1; }

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

    // re-read current values after the range queries (in case they got clobbered)
    mt2->GetGPUMinFrequency(&curMin);
    mt2->GetGPUMaxFrequency(&curMax);

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
