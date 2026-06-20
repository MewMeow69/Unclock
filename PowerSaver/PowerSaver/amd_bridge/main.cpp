#include <cstdio>
#include <cstring>

#include "ADLXHelper.h"
#include "IGPUManualGFXTuning.h"
#include "IGPUTuning.h"

using namespace adlx;

#define CHECK(x) do { auto r = (x); if (ADLX_FAILED(r)) { std::fprintf(stderr, "FAIL: %s\n  error=0x%X\n", #x, r); return 1; } } while(0)

static IADLXGPU* FirstGPU(IADLXSystem* sys)
{
    IADLXGPUList* gpus = nullptr;
    sys->GetGPUs(&gpus);
    if (!gpus) return nullptr;
    IADLXGPU* gpu = nullptr;
    if (gpus->Size() > 0) { gpus->At(0, &gpu); if (gpu) gpu->Acquire(); }
    gpus->Release();
    return gpu;
}

static int CmdInfo()
{
    ADLXHelper adlx;
    CHECK(adlx.Initialize());
    auto* sys = adlx.GetSystemServices();
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

static IADLXManualGraphicsTuning2* GetMT2(IADLXGPUTuningServices* tuning, IADLXGPU* gpu)
{
    IADLXInterface* ifc = nullptr;
    if (ADLX_FAILED(tuning->GetManualGFXTuning(gpu, &ifc)) || !ifc) return nullptr;
    IADLXManualGraphicsTuning2* mt2 = nullptr;
    ifc->QueryInterface(IADLXManualGraphicsTuning2::IID(), (void**)&mt2);
    ifc->Release();
    return mt2;
}

static int CmdPowerSave()
{
    ADLXHelper adlx;
    CHECK(adlx.Initialize());
    auto* sys = adlx.GetSystemServices();
    auto* gpu = FirstGPU(sys);
    if (!gpu) { std::fputs("FAIL: no GPU found\n", stderr); return 1; }

    IADLXGPUTuningServices* tuning = nullptr;
    CHECK(sys->GetGPUTuningServices(&tuning));

    auto* mt2 = GetMT2(tuning, gpu);
    if (!mt2) { std::fputs("FAIL: manual tuning not supported\n", stderr); tuning->Release(); gpu->Release(); adlx.Terminate(); return 1; }

    adlx_int freqMin;
    mt2->GetGPUMinFrequency(&freqMin);

    CHECK(mt2->SetGPUMinFrequency(freqMin));
    CHECK(mt2->SetGPUMaxFrequency(freqMin));

    std::printf("OK: GPU locked to %d MHz\n", (int)freqMin);

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
    auto* sys = adlx.GetSystemServices();
    auto* gpu = FirstGPU(sys);
    if (!gpu) { std::fputs("FAIL: no GPU found\n", stderr); return 1; }

    IADLXGPUTuningServices* tuning = nullptr;
    CHECK(sys->GetGPUTuningServices(&tuning));

    auto* mt2 = GetMT2(tuning, gpu);
    if (!mt2) { std::fputs("FAIL: manual tuning not supported\n", stderr); tuning->Release(); gpu->Release(); adlx.Terminate(); return 1; }

    adlx_int freqMin, freqMax;
    mt2->GetGPUMinFrequency(&freqMin);
    mt2->GetGPUMaxFrequency(&freqMax);

    adlx_int fMid = freqMin + (freqMax - freqMin) / 2;
    CHECK(mt2->SetGPUMinFrequency(freqMin));
    CHECK(mt2->SetGPUMaxFrequency(fMid));

    std::printf("OK: GPU max locked to %d MHz\n", (int)fMid);

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
    auto* sys = adlx.GetSystemServices();
    auto* gpu = FirstGPU(sys);
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
    auto* sys = adlx.GetSystemServices();
    auto* gpu = FirstGPU(sys);
    if (!gpu) { std::fputs("FAIL: no GPU found\n", stderr); return 1; }

    IADLXGPUTuningServices* tuning = nullptr;
    CHECK(sys->GetGPUTuningServices(&tuning));

    auto* mt2 = GetMT2(tuning, gpu);
    if (!mt2)
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

    // re-read current after range query
    mt2->GetGPUMinFrequency(&curMin);
    mt2->GetGPUMaxFrequency(&curMax);

    std::printf("{\"hwMin\":%d,\"hwMax\":%d,\"curMin\":%d,\"curMax\":%d}\n",
        (int)hwMin, (int)hwMax, (int)curMin, (int)curMax);

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
