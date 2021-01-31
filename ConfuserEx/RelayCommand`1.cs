using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Input;
using GalaSoft.MvvmLight.Helpers;

namespace ConfuserEx {
	internal class RelayCommand<T> : ICommand {
		private readonly WeakAction<T> _execute;
		private readonly WeakFunc<T, bool> _canExecute;

		/// <summary>
		/// Initializes a new instance of the RelayCommand class that 
		/// can always execute.
		/// </summary>
		/// <param name="execute">The execution logic. IMPORTANT: If the action causes a closure,
		/// you must set keepTargetAlive to true to avoid side effects. </param>
		/// <param name="keepTargetAlive">If true, the target of the Action will
		/// be kept as a hard reference, which might cause a memory leak. You should only set this
		/// parameter to true if the action is causing a closure. See
		/// http://galasoft.ch/s/mvvmweakaction. </param>
		/// <exception cref="ArgumentNullException">If the execute argument is null.</exception>
		public RelayCommand(Action<T> execute, bool keepTargetAlive = false)
			: this(execute, null, keepTargetAlive) {
		}

		/// <summary>
		/// Initializes a new instance of the RelayCommand class.
		/// </summary>
		/// <param name="execute">The execution logic. IMPORTANT: If the action causes a closure,
		/// you must set keepTargetAlive to true to avoid side effects. </param>
		/// <param name="canExecute">The execution status logic.  IMPORTANT: If the func causes a closure,
		/// you must set keepTargetAlive to true to avoid side effects. </param>
		/// <param name="keepTargetAlive">If true, the target of the Action will
		/// be kept as a hard reference, which might cause a memory leak. You should only set this
		/// parameter to true if the action is causing a closure. See
		/// http://galasoft.ch/s/mvvmweakaction. </param>
		/// <exception cref="ArgumentNullException">If the execute argument is null.</exception>
		public RelayCommand(Action<T> execute, Func<T, bool> canExecute, bool keepTargetAlive = false) {
			if (execute == null) {
				throw new ArgumentNullException("execute");
			}

			_execute = new WeakAction<T>(execute, keepTargetAlive);

			if (canExecute != null) {
				_canExecute = new WeakFunc<T, bool>(canExecute, keepTargetAlive);
			}
		}
		/// <summary>
		/// Occurs when changes occur that affect whether the command should execute.
		/// </summary>
		public event EventHandler CanExecuteChanged {
			add {
				if (!(_canExecute is null))
					CommandManager.RequerySuggested += value;
			}

			remove {
				if (!(_canExecute is null))
					CommandManager.RequerySuggested -= value;
			}
		}

		/// <summary>
		/// Raises the <see cref="CanExecuteChanged" /> event.
		/// </summary>
		[SuppressMessage(
			"Microsoft.Performance",
			"CA1822:MarkMembersAsStatic",
			Justification = "The this keyword is used in the Silverlight version")]
		public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();

		/// <summary>
		/// Defines the method that determines whether the command can execute in its current state.
		/// </summary>
		/// <param name="parameter">Data used by the command. If the command does not require data 
		/// to be passed, this object can be set to a null reference</param>
		/// <returns>true if this command can be executed; otherwise, false.</returns>
		public bool CanExecute(object parameter) {
			if (_canExecute is null) {
				return true;
			}

			if (_canExecute.IsStatic || _canExecute.IsAlive) {
				if (parameter == null
					&& typeof(T).IsValueType) {
					return _canExecute.Execute(default);
				}

				if (parameter == null || parameter is T) {
					return (_canExecute.Execute((T)parameter));
				}
			}

			return false;
		}

		/// <summary>
		/// Defines the method to be called when the command is invoked. 
		/// </summary>
		/// <param name="parameter">Data used by the command. If the command does not require data 
		/// to be passed, this object can be set to a null reference</param>
		public virtual void Execute(object parameter) {
			var val = parameter;

			if (parameter != null
				&& parameter.GetType() != typeof(T)) {
				if (parameter is IConvertible) {
					val = Convert.ChangeType(parameter, typeof(T), null);
				}
			}

			if (CanExecute(val)
				&& _execute != null
				&& (_execute.IsStatic || _execute.IsAlive)) {
				if (val == null) {
					if (typeof(T).IsValueType) {
						_execute.Execute(default);
					}
					else {
						// ReSharper disable ExpressionIsAlwaysNull
						_execute.Execute((T)val);
						// ReSharper restore ExpressionIsAlwaysNull
					}
				}
				else {
					_execute.Execute((T)val);
				}
			}
		}
	}
}
