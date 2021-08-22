#include <string>

using namespace System;

static std::wstring GetFromCpp() {
	return L"From native method";
}

int main()
{
	Console::WriteLine("START");
	Console::WriteLine(gcnew String(GetFromCpp().c_str()));
	Console::WriteLine("END");
    return 42;
}
