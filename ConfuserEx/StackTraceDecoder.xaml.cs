using System.Windows;
using Confuser.Renamer;
using Ookii.Dialogs.Wpf;

namespace ConfuserEx {
	/// <summary>
	///     Interaction logic for StackTraceDecoder.xaml
	/// </summary>
	public partial class StackTraceDecoder {
		MessageDeobfuscator _messageDeobfuscator;

		public StackTraceDecoder() => InitializeComponent();

		void ChooseMapPath(object sender, RoutedEventArgs e) {
			var ofd = new VistaOpenFileDialog();
			ofd.Filter = "Symbol maps (*.map)|*.map|All Files (*.*)|*.*";
			if (ofd.ShowDialog() ?? false) {
				PathBox.Text = ofd.FileName;
			}
		}

		void Decode_Click(object sender, RoutedEventArgs e) {
			bool error = false;
			if (optSym.IsChecked ?? true) {
				var path = PathBox.Text.Trim(' ', '"');
				string shortPath = path;
				if (path.Length > 35)
					shortPath = "..." + path.Substring(path.Length - 35, 35);

				try {
					_messageDeobfuscator = MessageDeobfuscator.Load(path);
					status.Content = "Loaded symbol map from '" + shortPath + "' successfully.";
				}
				catch {
					status.Content = "Failed to load symbol map from '" + shortPath + "'.";
					error = true;
				}
			}
			else {
				_messageDeobfuscator = new MessageDeobfuscator(PassBox.Password);
			}

			if (!error) {
				stackTrace.Text = _messageDeobfuscator.DeobfuscateMessage(stackTrace.Text);
			}
		}
	}
}
