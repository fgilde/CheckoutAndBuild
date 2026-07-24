using System;
using System.Diagnostics;
using System.Windows.Media;

namespace CheckoutAndBuild2.Contracts
{
	[DebuggerDisplay("{StatusText}")]
	public class OperationInfo : NotificationObject, IEquatable<OperationInfo>
	{
		private readonly int id;
		private string statusText;
		private Brush colorBrush;
		private bool isIndeterminate;
		private double progress;

		/// <summary>
		/// Initializes a new instance of the <see cref="NotificationObject"/> class.
		/// </summary>
		public OperationInfo(int id)
		{
			this.id = id;
			ColorBrush = Brushes.Green;
		}

		public double Progress
		{
			get { return progress; }
			set
			{
				SetProperty(ref progress, value);
				IsIndeterminate = value >= 100 || value <= 0;
			}
		}

		public string StatusText
		{
			get { return statusText; }
			set { SetProperty(ref statusText, value); }
		}


		public Brush ColorBrush
		{
			get { return colorBrush; }
			set { SetProperty(ref colorBrush, value); }
		}

		public bool IsIndeterminate
		{
			get { return isIndeterminate; }
			set { SetProperty(ref isIndeterminate, value); }
		}

		public void SetProgress(double total, double value)
		{
			Progress = (value / total) * 100.0;
		}

		#region equality

		/// <summary>
		/// Indicates whether the current object is equal to another object of the same type.
		/// </summary>
		/// <returns>
		/// true if the current object is equal to the <paramref name="other"/> parameter; otherwise, false.
		/// </returns>
		/// <param name="other">An object to compare with this object.</param>
		public bool Equals(OperationInfo other)
		{
			if (ReferenceEquals(null, other)) return false;
			if (ReferenceEquals(this, other)) return true;
			return id == other.id;
		}

		/// <summary>
		/// Determines whether the specified <see cref="T:System.Object"/> is equal to the current <see cref="T:System.Object"/>.
		/// </summary>
		/// <returns>
		/// true if the specified object  is equal to the current object; otherwise, false.
		/// </returns>
		/// <param name="obj">The object to compare with the current object. </param>
		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((OperationInfo) obj);
		}

		/// <summary>
		/// Serves as a hash function for a particular type. 
		/// </summary>
		/// <returns>
		/// A hash code for the current <see cref="T:System.Object"/>.
		/// </returns>
		public override int GetHashCode()
		{
			return id;
		}

		public static bool operator ==(OperationInfo left, OperationInfo right)
		{
			return Equals(left, right);
		}

		public static bool operator !=(OperationInfo left, OperationInfo right)
		{
			return !Equals(left, right);
		}

		#endregion

	}

	public static class Operations
	{

		public static OperationInfo None => null;
	    public static OperationInfo Checkout => new OperationInfo(1) { StatusText = "Checkout", IsIndeterminate = false };
	    public static OperationInfo Cancelling => new OperationInfo(2) { StatusText = "Cancelling", IsIndeterminate = true, ColorBrush = Brushes.Brown };
	    public static OperationInfo Queued => new OperationInfo(3) { StatusText = "Queued", IsIndeterminate = true, ColorBrush = Brushes.Orange };
	    public static OperationInfo Build => new OperationInfo(4) { StatusText = "Building", IsIndeterminate = false };
	    public static OperationInfo BuildIndeterminate => new OperationInfo(4) { StatusText = "Building", IsIndeterminate = true };
	    public static OperationInfo Clean => new OperationInfo(5) { StatusText = "Cleaning", IsIndeterminate = false };
	    public static OperationInfo UnitTesting => new OperationInfo(6) { StatusText = "Run Unit tests", IsIndeterminate = false };
	    public static OperationInfo UnitTestingIndeterminate => new OperationInfo(6) { StatusText = "Run Unit tests", IsIndeterminate = true };
	    public static OperationInfo Starting => new OperationInfo(7) { StatusText = "Starting", IsIndeterminate = true, ColorBrush = Brushes.Azure};
	    public static OperationInfo Stopping => new OperationInfo(8) { StatusText = "Stopping", IsIndeterminate = true, ColorBrush = Brushes.Brown };
	    public static OperationInfo NugetRestore => new OperationInfo(9) { StatusText = "Nuget Restore", IsIndeterminate = true, ColorBrush = Brushes.Aquamarine };
	    public static OperationInfo Paused => new OperationInfo(10) { StatusText = "!! PAUSED !!", IsIndeterminate = false, Progress = 1, ColorBrush = Brushes.Orange };

        
    }

}