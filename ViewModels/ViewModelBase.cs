using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace HocrEditor.ViewModels
{
    public abstract class ViewModelBase : INotifyPropertyChanged, IDisposable
    {
        #region IsChanged

        public bool IsChanged { get; set; }

        public virtual void MarkAsUnchanged()
        {
            IsChanged = false;
        }

        #endregion

        #region PropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;


        [NotifyPropertyChangedInvocator]
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region Disposables

        public abstract void Dispose();

        #endregion

    }
}
