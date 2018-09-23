using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Confuser.Core.Parameter;

namespace Confuser.Core {
	public static class ProtectionParameter {
		public static IProtectionParameter<bool> Boolean(string name, bool defaultValue) {
			CheckName(name);

			return new BooleanProtectionParameter(name, defaultValue);
		}

		public static IProtectionParameter<string> String(string name, string defaultValue) {
			CheckName(name);

			return new StringProtectionParameter(name, defaultValue);
		}

		public static IProtectionParameter<uint> UInteger(string name, uint defaultValue) {
			CheckName(name);

			return new UnsignedIntegerProtectionParameter(name, defaultValue);
		}

		public static IProtectionParameter<int> Integer(string name, int defaultValue) {
			CheckName(name);

			return new IntegerProtectionParameter(name, defaultValue);
		}

		public static IProtectionParameter<double> Percent(string name, double defaultValue) {
			CheckName(name);
			if (defaultValue < 0.0 || defaultValue > 1.0)
				throw new ArgumentOutOfRangeException(nameof(defaultValue), defaultValue, "The default value is expected to be within the legal range of 0 to 1.");

			return new PercentProtectionParameter(name, defaultValue);
		}

		public static  IProtectionParameter<T> Enum<T>(string name, T defaultValue) where T:struct {
			CheckName(name);
			if (!typeof(T).IsEnum) throw new ArgumentException("Type is expected to be a enum.", nameof(T));
			if (!System.Enum.IsDefined(typeof(T), defaultValue)) throw new ArgumentException("The default value has to be defined by the enum.", nameof(defaultValue));

			return new EnumProtectionParameter<T>(name, defaultValue);
		}

		private static void CheckName(string name) {
			if (name == null) throw new ArgumentNullException(nameof(name));
			if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Parameter name must not be empty or white-space.", nameof(name));
		}
	}
}
