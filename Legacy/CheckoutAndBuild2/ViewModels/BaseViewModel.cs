using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CheckoutAndBuild2.Contracts;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.Services;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Common.Internal;
using Microsoft.TeamFoundation.Controls;
using Microsoft.TeamFoundation.Controls.WPF.TeamExplorer;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace FG.CheckoutAndBuild2.ViewModels
{
    public abstract class BaseViewModel : NotificationObject
    {
        protected readonly IServiceProvider serviceProvider;
        private bool isConnected;
        private bool isSelected;
        private bool isBusy;
        protected object lockObj = new object();

        public PausableCancellationTokenSource ModelCancellationTokenSource { get; private set; }

        public TeamExplorerUtils TeamExplorerUtils => TeamExplorerUtils.Instance;

        public bool IsSelected
        {
            get => isSelected;
            set => SetProperty(ref isSelected, value);
        }


        public bool IsBusy
        {
            get => isBusy;
            protected set => SetProperty(ref isBusy, value);
        }

        public bool HasWorkspaceOrDirectory
        {
            get
            {
                if (TfsContext == null)
                    return false;
                return HasWorkspace || Directory.Exists(TfsContext.SelectedDirectory);
            }
        }

        public bool HasWorkspace
        {
            get
            {
                var tfsContext = TfsContext;
                return tfsContext?.SelectedWorkspace != null;
            }
        }

        public bool IsConnected
        {
            get => isConnected;
            set => SetProperty(ref isConnected, value);
        }

        protected BaseViewModel(IServiceProvider serviceProvider)
        {
            CreateCancellationToken();
            this.serviceProvider = serviceProvider;
            var tfsContext = TfsContext;
            IsConnected = tfsContext != null && tfsContext.IsTfsConnected;
            if (tfsContext != null)
            {
                tfsContext.ConnectionChanged += ConnectionChanged;
                tfsContext.SelectedWorkspaceChanged += SelectedWorkspaceChanged;
            }
        }

        private void CreateCancellationToken()
        {
            ModelCancellationTokenSource = new PausableCancellationTokenSource();
            ModelCancellationTokenSource.Token.Register(() =>{
                    IsBusy = false;
                    ModelCancellationTokenSource = null;
                    CreateCancellationToken();
                }
            );
        }

        public virtual void Cancel()
        {
            ModelCancellationTokenSource?.Cancel();
        }

        public virtual bool CanCancel()
        {
            return ModelCancellationTokenSource != null
                    && !ModelCancellationTokenSource.IsCancellationRequested
                    && !ModelCancellationTokenSource.Token.IsCancellationRequested;
        }

        private void SelectedWorkspaceChanged(object sender, EventArgs<Workspace> eventArgs)
        {
            RaisePropertiesChanged(() => HasWorkspaceOrDirectory, () => HasWorkspace);
        }

        protected virtual void ConnectionChanged(object sender, ContextChangedEventArgs e)
        {
            lock (lockObj)
            {
                var tfsContext = TfsContext;
                if (tfsContext != null && tfsContext.IsTfsConnected)
                {
                    IsConnected = true;
                    Update();
                }
                else
                {
                    IsConnected = false;
                    Unload();
                }
            }
        }

        public virtual void Update()
        {
            if (!IsBusy)
            {
                IsBusy = true;
                Task.Factory.StartNew(() => OnUpdateAsynchronous(ModelCancellationTokenSource.Token), ModelCancellationTokenSource.Token).ContinueWith(task => System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    OnUpdateSynchronous(ModelCancellationTokenSource.Token);
                    IsBusy = false;
                }), ModelCancellationTokenSource.Token);
            }
        }

        protected virtual void OnUpdateSynchronous(CancellationToken cancellationToken)
        { }

        protected virtual void OnUpdateAsynchronous(CancellationToken cancellationToken)
        { }

        protected virtual void Unload()
        { }

        public ITeamExplorer TeamExplorer => GetService<ITeamExplorer>();

        public TfsContext TfsContext => GetService<TfsContext>();

        public SettingsService Settings => GetService<SettingsService>();


        protected T GetService<T>()
        {
            //TeamExplorer.CurrentPage.GetService<>()
            //return serviceProvider.GetService<T>();
            return serviceProvider.Get<T>();
        }
    }
}