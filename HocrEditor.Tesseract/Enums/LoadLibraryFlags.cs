namespace HocrEditor.Tesseract;

[Flags]
internal enum LoadLibraryFlags : uint
{
    None = 0,
    DontResolveDllReferences = 0x00000001,
    LoadIgnoreCodeAuthorizationLevel = 0x00000010,
    LoadLibraryAsDatafile = 0x00000002,
    LoadLibraryAsDatafileExclusive = 0x00000040,
    LoadLibraryAsImageResource = 0x00000020,
    LoadLibrarySearchApplicationDir = 0x00000200,
    LoadLibrarySearchDefaultDirs = 0x00001000,
    LoadLibrarySearchDllLoadDir = 0x00000100,
    LoadLibrarySearchSystem32 = 0x00000800,
    LoadLibrarySearchUserDirs = 0x00000400,
    LoadWithAlteredSearchPath = 0x00000008
}
