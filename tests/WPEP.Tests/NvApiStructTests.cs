using WPEP.SystemAnalyzer;
using Xunit;

namespace WPEP.Tests;

public class NvApiStructTests
{
    // REGRESSIONE: la struct NVDRS_SETTING deve marshallare ESATTAMENTE alla sizeof nativa.
    // version = sizeof | (1<<16); se la dimensione è sbagliata NVAPI rifiuta con -9
    // (INCOMPATIBLE_STRUCT_VERSION) e read+write del pannello NVIDIA falliscono in silenzio.
    // Il bug originale: union da 4104 (un NvU32 di troppo) invece di 4100 → +8 byte → -9.
    // Native: 4(version) + 4096(settingName) + 5*4(id/type/location/2 flag) + 2*4100(union) = 12320.
    [Fact]
    public void NvdrsSetting_MarshalsToNativeSize_12320()
    {
        Assert.Equal(12320, NvApi.NvdrsSettingMarshalSize);
    }
}
