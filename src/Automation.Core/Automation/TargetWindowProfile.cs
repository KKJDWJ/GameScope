using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Automation.Core.Windows;

namespace Automation.Core.Automation
{
    public sealed class TargetWindowProfile : INotifyPropertyChanged
    {
        private string _activationState;

        public TargetWindowProfile()
        {
            Aois = new List<AoiDefinition>();
            IsEnabled = true;
        }

        public string Name { get; set; }
        public WindowInfo TargetWindow { get; set; }
        public string WindowTitle { get; set; }
        public string WindowClassName { get; set; }
        public bool IsEnabled { get; set; }
        public List<AoiDefinition> Aois { get; private set; }

        [JsonIgnore]
        public string ActivationState
        {
            get { return _activationState; }
            set
            {
                if (_activationState == value)
                {
                    return;
                }

                _activationState = value;
                OnPropertyChanged();
            }
        }

        public string Title
        {
            get { return TargetWindow == null ? (WindowTitle ?? string.Empty) : TargetWindow.Title; }
        }

        public string ClassName
        {
            get { return TargetWindow == null ? (WindowClassName ?? string.Empty) : TargetWindow.ClassName; }
        }

        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(Name) ? Title : Name;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
