using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using FG.CheckoutAndBuild2.Common.Commands;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.Properties;
using FG.CheckoutAndBuild2.Services;
using Microsoft.TeamFoundation.VersionControl.Client;


namespace FG.CheckoutAndBuild2.Controls
{
    /// <summary>
    /// Interaction logic for NameEdit.xaml
    /// </summary>
    public partial class WorkspaceSettingsCopyControl
    {
        private readonly Action<WorkspaceSettingsCopyControl, bool> closeAction;

        #region Dependency

        // Using a DependencyProperty as the backing store for IsLoading.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsLoadingProperty =
            DependencyProperty.Register("IsLoading", typeof(bool), typeof(WorkspaceSettingsCopyControl), new PropertyMetadata(false));

        
        // Using a DependencyProperty as the backing store for SourceWorkspace.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SourceWorkspaceProperty =
            DependencyProperty.Register("SourceWorkspace", typeof(Workspace), typeof(WorkspaceSettingsCopyControl),
                new PropertyMetadata(null, SourceWorkspaceChanged));

        private static void SourceWorkspaceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var self = (WorkspaceSettingsCopyControl)d;
            self.CopyCommand.RaiseCanExecuteChanged();
        }


        // Using a DependencyProperty as the backing store for TargetWorkspace.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TargetWorkspaceProperty =
            DependencyProperty.Register("TargetWorkspace", typeof(Workspace), typeof(WorkspaceSettingsCopyControl),
                new PropertyMetadata(null, TargetWorkspaceChanged));

        private static void TargetWorkspaceChanged(DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs dependencyPropertyChangedEventArgs)
        {
            var self = (WorkspaceSettingsCopyControl)dependencyObject;
            self.UpdateAvailableWorkspaces();
        }


        // Using a DependencyProperty as the backing store for Message.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register("Message", typeof(string), typeof(WorkspaceSettingsCopyControl),
                new PropertyMetadata(Texts.DefaultCopySettingsHeader));


        #endregion
        
        public DelegateCommand<object> CopyCommand { get; set; }
        public DelegateCommand<object> CancelCommand { get; set; }

        private readonly TfsContext tfsContext;
        private readonly SettingsService settingsService;

        public WorkspaceSettingsCopyControl(IServiceProvider serviceProvider, Action<WorkspaceSettingsCopyControl, bool> closeAction)
        {
            this.closeAction = closeAction;
            tfsContext = serviceProvider.Get<TfsContext>();
            settingsService = serviceProvider.Get<SettingsService>();
            CopyCommand = new DelegateCommand<object>(ExecuteCopyMethod, CanCopy);
			CancelCommand = new DelegateCommand<object>(ExecuteCancelMethod);
            AvailableWorkspaces = new ObservableCollection<Workspace>();
            UpdateAvailableWorkspaces();
            InitializeComponent();
			DataContext = this;
		}


        public ObservableCollection<Workspace> AvailableWorkspaces { get; }



        public bool IsLoading
        {
            get { return (bool)GetValue(IsLoadingProperty); }
            set { SetValue(IsLoadingProperty, value); }
        }

        public string Message
        {
            get { return (string)GetValue(MessageProperty); }
            set { SetValue(MessageProperty, value); }
        }
        

        public Workspace TargetWorkspace
        {
            get { return (Workspace)GetValue(TargetWorkspaceProperty); }
            set { SetValue(TargetWorkspaceProperty, value); }
        }

        public Workspace SourceWorkspace
        {
            get { return (Workspace)GetValue(SourceWorkspaceProperty); }
            set { SetValue(SourceWorkspaceProperty, value); }
        }

        public void Close(bool hasCopiedSettings)
        {
            closeAction?.Invoke(this, hasCopiedSettings);
        }
        
        private async void UpdateAvailableWorkspaces()
        {            
            IEnumerable<Workspace> workspacesWithSettings = await Task.Run(() => tfsContext.GetWorkspaces().Where(w => w != TargetWorkspace && settingsService.HasSettings(tfsContext.SelectedProfile, w)));
            AvailableWorkspaces.Clear();
            AvailableWorkspaces.AddRange(workspacesWithSettings.Distinct().ToList());
            CopyCommand.RaiseCanExecuteChanged();            
        }

        private bool CanCopy(object arg)
        {
            return SourceWorkspace != null && TargetWorkspace != null
                   && SourceWorkspace != TargetWorkspace;
        }


        private void ExecuteCancelMethod(object obj)
		{
		    Close(false);
		}

	
		private async void ExecuteCopyMethod(object o)
		{
		    IsLoading = true;		    		    
            Close(await settingsService.CopySettingsAsync(tfsContext.SelectedProfile, SourceWorkspace, tfsContext.SelectedProfile, TargetWorkspace));
            IsLoading = false;
        }

        private void CloseClick(object sender, RoutedEventArgs e)
        {
            Close(false);
        }
    }
}
