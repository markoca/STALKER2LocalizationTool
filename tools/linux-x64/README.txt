Reserved for a future native Linux application target.

The current STALKER2LocalizationTool is a Windows Forms application targeting net8.0-windows.
Do not put Linux helper binaries into a Windows release. When publishing win-x64 from Linux,
the publish script uses tools/win-x64/ because the TARGET is Windows.
