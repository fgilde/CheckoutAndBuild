using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;
using CheckoutAndBuild2.Contracts;
using CheckoutAndBuild2.Contracts.Service;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.Properties;
using Microsoft.VisualStudio.Shell;

namespace FG.CheckoutAndBuild2.ViewModels
{
	public class BuildErrorsViewModel : BaseViewModel
	{
		private string title;
		private string message;
		private ImageSource image;
		private ErrorTask selectedError;

		public BuildErrorsViewModel(IEnumerable<ErrorTask> tasks, IOperationService requestedOperation, ISolutionProjectModel project,
			IServiceProvider serviceProvider) : base(serviceProvider)
		{
			Errors = new ObservableCollection<ErrorTask>(tasks.ToList());
			Title = string.Format("{1} Error for '{0}'", project.SolutionFileName, requestedOperation.OperationName);						
			Message = string.Format("{0} errors for '{1}' occured", Errors.Count, project.SolutionFileName);
			RequestedOperation = requestedOperation;
			Project = project;
			Image = Images.BuildErrorList_7237.ToImageSource();
		}

		public ImageSource Image
		{
			get { return image; }
			set { SetProperty(ref image, value); }
		}


		public ErrorTask SelectedError
		{
			get { return selectedError; }
			set { SetProperty(ref selectedError, value); }
		}

		public string Title
		{
			get { return title; }
			set { SetProperty(ref title, value); }
		}
		
		public string Message
		{
			get { return message; }
			set { SetProperty(ref message, value); }
		}

		public IOperationService RequestedOperation { get; private set; }
		public ISolutionProjectModel Project { get; private set; }
		public ObservableCollection<ErrorTask> Errors { get; private set; }

	}
}