$unityPath = "E:\GameDev\Unity\2022.3.35f1\Editor\Unity.exe"
$projectPath = "E:\GameDev\Modding\Dark.RimWorldMods\AssetBundleProject\"
$assetBundlePath = "E:\GameDev\Modding\Dark.RimWorldMods\AssetBundleProject\Assets\AssetBundles\"

try
{
    Get-ChildItem -Path $assetBundlePath -File | Remove-Item -Force

    Write-Host "Starting Unity in batchmode..."
    # Start Unity process
    Start-Process -FilePath $unityPath -ArgumentList "-batchmode", "-quit", "-projectPath", "$projectPath", "-executeMethod", "ModAssetBundleBuilder.BuildBundles", "--assetBundleName=dark_betterletters" -NoNewWindow
    Write-Host "Unity is building assetbundle(s)"


    Start-Process "explorer.exe" "`"$assetBundlePath`""
}
catch
{
    Write-Error "An error occurred: $_"
}

