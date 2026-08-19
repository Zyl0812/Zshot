namespace Starshot.Core.Ocr;

public sealed class OcrModelPaths
{
    public required string Root { get; init; }
    public required string Detect { get; init; }
    public required string Recognize { get; init; }
    public required string? Classifier { get; init; }
    public bool Exists => File.Exists(Detect) && File.Exists(Recognize);
}

public sealed class OcrModelManager
{
    private readonly string _root;

    public OcrModelManager(string? localAppData = null)
    {
        string baseDir = localAppData ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Zshot");
        _root = Path.Combine(baseDir, "models", "ocr");
    }

    public OcrModelPaths GetPaths(OcrAccuracy accuracy)
    {
        string folder = accuracy is OcrAccuracy.High ? "ppocrv6-medium" : "ppocrv6-small";
        string dir = Path.Combine(_root, folder);
        return new OcrModelPaths
        {
            Root = dir,
            Detect = Path.Combine(dir, "det.onnx"),
            Recognize = Path.Combine(dir, "rec.onnx"),
            Classifier = File.Exists(Path.Combine(dir, "cls.onnx")) ? Path.Combine(dir, "cls.onnx") : null,
        };
    }
}
