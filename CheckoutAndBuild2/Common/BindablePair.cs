using System;
using System.Collections.Generic;
using CheckoutAndBuild2.Contracts;

namespace FG.CheckoutAndBuild2.Common
{
	public class BindablePair<TKey, TValue> : NotificationObject
	{
		public event EventHandler Changed;

		private TKey key;
		private TValue _value;

		public TKey Key
		{
			get { return key; }
			set
			{
				if(SetProperty(ref key, value))
					RaiseChanged();
			}
		}

		public TValue Value
		{
			get { return _value; }
			set
			{
				if(SetProperty(ref _value, value))
					RaiseChanged();
			}
		}

		public BindablePair() { }
		public BindablePair(TKey key, TValue value)
			: this()
		{
			Key = key;
			Value = value;
		}

		public static implicit operator KeyValuePair<TKey, TValue>(BindablePair<TKey, TValue> pair )
		{
			return new KeyValuePair<TKey, TValue>(pair.Key, pair.Value);
		}

		public static implicit operator BindablePair<TKey, TValue>(KeyValuePair<TKey, TValue> pair)
		{
			return new BindablePair<TKey, TValue>(pair.Key, pair.Value);
		}


		private void RaiseChanged()
		{
			var handler = Changed;
			if (handler != null) handler(this, EventArgs.Empty);
		}

	}

	public class Bindable<TValue> : NotificationObject
	{
		public event EventHandler Changed;

		private TValue _value;
		
		public TValue Value
		{
			get { return _value; }
			set
			{
				if (SetProperty(ref _value, value))
					RaiseChanged();
			}
		}

		public Bindable() { }
		public Bindable(TValue value)
			: this()
		{			
			Value = value;
		}

		public static implicit operator TValue(Bindable<TValue> value)
		{
			return value.Value;
		}

		public static implicit operator Bindable<TValue>(TValue value)
		{
			return new Bindable<TValue>(value);
		}


		private void RaiseChanged()
		{
			var handler = Changed;
			if (handler != null) handler(this, EventArgs.Empty);
		}

	}

}