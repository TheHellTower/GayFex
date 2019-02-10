using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Confuser.Core;

namespace ConfuserEx {
	public partial class CompComboBox : UserControl {
		public static readonly DependencyProperty ComponentsProperty = DependencyProperty.Register("Components",
			typeof(IEnumerable<ConfuserUiComponent>), typeof(CompComboBox), new UIPropertyMetadata(null));

		public static readonly DependencyProperty SelectedComponentProperty =
			DependencyProperty.Register("SelectedComponent", typeof(ConfuserUiComponent), typeof(CompComboBox),
				new UIPropertyMetadata(null));

		public static readonly DependencyProperty ArgumentsProperty = DependencyProperty.Register("Arguments",
			typeof(Dictionary<string, string>), typeof(CompComboBox), new UIPropertyMetadata(null));

		public CompComboBox() {
			InitializeComponent();
		}

		public IEnumerable<ConfuserUiComponent> Components {
			get { return (IEnumerable<ConfuserUiComponent>)GetValue(ComponentsProperty); }
			set { SetValue(ComponentsProperty, value); }
		}

		public ConfuserUiComponent SelectedComponent {
			get { return (ConfuserUiComponent)GetValue(SelectedComponentProperty); }
			set { SetValue(SelectedComponentProperty, value); }
		}

		public Dictionary<string, string> Arguments {
			get { return (Dictionary<string, string>)GetValue(ArgumentsProperty); }
			set { SetValue(ArgumentsProperty, value); }
		}
	}
}
