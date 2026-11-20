using System.Runtime.InteropServices;

[assembly: Guid("6a1c5b1e-3f0d-4c2a-9b7e-2d1f4e8a9c01")]
[assembly: ImportedFromTypeLib("PinInterop")]

namespace PinInterop
{
    [ComImport, Guid("0f3e2d1c-5b4a-4978-8665-544332211000"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IThing
    {
        int Count();
    }
}
