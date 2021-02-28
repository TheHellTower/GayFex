using System;

namespace Confuser.Optimizations.Runtime
{
    public readonly ref struct ReadOnlyStringView {
	    private readonly string _value;
	    private readonly int _start;
	    private readonly int _length;

	    public int Length => _length;

	    public unsafe ref readonly char this[int index] {
		    get {
			    fixed (char* p = _value) 
				    return ref *(p + _start + index);
		    }
	    }

	    internal ReadOnlyStringView(string value, int start, int length) {
		    _value = value;
		    _start = start;
		    _length = length;
	    }

	    public int IndexOf(char value) {
		    var index = _value.IndexOf(value, _start, _length);
		    if (index == -1) return -1;
		    return index - _start;
	    }

	    public int IndexOfAny(char value1, char value2) {
		    var index = _value.IndexOfAny(new[] {value1, value2}, _start, _length);
		    if (index == -1) return -1;
		    return index - _start;
	    }

	    public ReadOnlyStringView Slice(int start) => 
		    new ReadOnlyStringView(_value, _start + start, _length - start);

	    public unsafe ref char GetReference() {
		    fixed (char* p = _value) 
			    return ref *(p + _start);
	    }
    }
}
