﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace TypeScrambler {
	internal class GenericClass<T> where T : IEnumerable<Char> {
		public IEnumerable<Char> GetReverse(T input) => input.Reverse();
	}
}
