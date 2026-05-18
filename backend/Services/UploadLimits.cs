namespace Eden_Relics_BE.Services;

public static class UploadLimits
{
    public const long MaxUploadBytes = 4L * 1024 * 1024 * 1024;

    public const string MaxUploadDisplay = "4 GB";

    public const long ImageResizeWarnBytes = 10L * 1024 * 1024;

    public const long LogoResizeWarnBytes = 5L * 1024 * 1024;
}
