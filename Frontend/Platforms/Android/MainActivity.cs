using Android.App;
using Android.Content.PM;
using Android.OS;
using Microsoft.Maui;

namespace SOE
{
    [Activity(
        Name = "com.companyname.soe.MainActivity", // Должно совпадать с AndroidManifest
        Theme = "@style/Maui.SplashTheme",
        MainLauncher = true,
        ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation |
                              ConfigChanges.UiMode | ConfigChanges.ScreenLayout |
                              ConfigChanges.SmallestScreenSize | ConfigChanges.Density,
        Exported = true)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
        }
    }
}