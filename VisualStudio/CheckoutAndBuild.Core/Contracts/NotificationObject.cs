using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace CheckoutAndBuild.Core.Contracts
{
	/// <summary>
	///   <see cref="INotifyPropertyChanged" /> und <see cref="INotifyPropertyChanging" /> implementierung
	/// </summary>
	public abstract class NotificationObject : INotifyPropertyChanged, INotifyPropertyChanging, ICloneable
	{
		private bool isNotifying;

		protected NotificationObject()
		{
			IsNotifying = true;
		}

		public bool IsNotifying
		{
			get { return isNotifying; }
			set
			{
				isNotifying = value;
				RaiseIsNotifyingChanged();
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		public event PropertyChangingEventHandler PropertyChanging;

		public event EventHandler IsNotifyingChanged;

		public virtual void RaisePropertiesChanged(params Expression<Func<object>>[] actions)
		{
			foreach (var expression in actions)
				RaisePropertyChanged(expression);
		}

		public virtual void RaisePropertiesChanging(params Expression<Func<object>>[] actions)
		{
			foreach (var expression in actions)
				RaisePropertyChanging(expression);
		}

		/// <summary>
		/// Called when [property changed].
		/// </summary>
		public virtual void RaisePropertyChanged(Expression<Func<object>> action)
		{
			RaisePropertyChanged(GetMemberName(action));
		}

		/// <summary>
		/// Called when [property changing].
		/// </summary>
		public virtual void RaisePropertyChanging(Expression<Func<object>> action)
		{
			RaisePropertyChanging(GetMemberName(action));
		}

		protected virtual void RaiseAllPropertiesChanged()
		{
			var properties = GetType().GetProperties();
			foreach (PropertyInfo property in properties)
				RaisePropertyChanged(property.Name);
		}

		protected virtual void RaiseAllPropertiesChanging()
		{
			var properties = GetType().GetProperties();
			foreach (PropertyInfo property in properties)
				RaisePropertyChanging(property.Name);
		}

		/// <summary>
		/// Raises the property changing.
		/// </summary>
		public void RaisePropertyChanging([CallerMemberName]string propertyName = null)
		{
			PropertyChangingEventHandler handler = PropertyChanging;
			if (handler != null && IsNotifying)
				handler(this, new PropertyChangingEventArgs(propertyName));
		}

		/// <summary>
		/// Raises the property changed.
		/// </summary>
		public void RaisePropertyChanged([CallerMemberName]string propertyName = null)
		{
			PropertyChangedEventHandler handler = PropertyChanged;
			if (handler != null && IsNotifying)
				handler(this, new PropertyChangedEventArgs(propertyName));
		}

		private void RaiseIsNotifyingChanged()
		{
			EventHandler handler = IsNotifyingChanged;
			if (handler != null) handler(this, EventArgs.Empty);
		}

		protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName]string propertyName = null)
		{
			if (Equals(storage, value))
				return false;
			RaisePropertyChanging(propertyName);
			storage = value;
			RaisePropertyChanged(propertyName);
			return true;
		}

		/// <summary>
		/// Creates a new object that is a copy of the current instance.
		/// </summary>
		/// <returns>
		/// A new object that is a copy of this instance.
		/// </returns>
		/// <filterpriority>2</filterpriority>
		public virtual object Clone()
		{
			return MemberwiseClone();
		}

		[SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Not used in all design time assemblies.")]
		private static string GetMemberName<T>(Expression<Func<T>> expr)
		{
			Expression body = expr.Body;
			if (body is MemberExpression || body is UnaryExpression)
			{
				MemberExpression memberExpression = body as MemberExpression ?? (MemberExpression)((UnaryExpression)body).Operand;
				return memberExpression.Member.Name;
			}

			return expr.ToString();
		}

	}
}
