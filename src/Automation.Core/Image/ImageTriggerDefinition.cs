namespace Automation.Core.Image
{
    public sealed class ImageTriggerDefinition
    {
        public ImageTriggerDefinition(string name, string imagePath, double threshold)
        {
            Name = name;
            ImagePath = imagePath;
            Threshold = threshold;
            IsEnabled = true;
        }

        public string Name { get; private set; }
        public string ImagePath { get; private set; }
        public double Threshold { get; set; }
        public bool IsEnabled { get; set; }
    }
}
