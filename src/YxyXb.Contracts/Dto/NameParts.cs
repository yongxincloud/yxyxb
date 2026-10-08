namespace YxyXb.Contracts.Dto
{
    public sealed class NameParts
    {
        public const int DefaultFlags = 191;

        public const int FileSoftwareId = 1;
        public const int FileTask = 2;
        public const int FileTime = 4;
        public const int FileDb = 8;
        public const int DirSoftwareId = 16;
        public const int DirKind = 32;
        public const int DirInstance = 64;
        public const int DirTask = 128;

        public const int FileMask = FileSoftwareId | FileTask | FileTime | FileDb;

        public bool UseFileSoftwareId { get; set; }

        public bool UseFileTask { get; set; }

        public bool UseFileTime { get; set; }

        public bool UseFileDb { get; set; }

        public bool UseDirSoftwareId { get; set; }

        public bool UseDirKind { get; set; }

        public bool UseDirInstance { get; set; }

        public bool UseDirTask { get; set; }

        public static NameParts Default
        {
            get { return FromFlags(DefaultFlags); }
        }

        public static NameParts FromFlags(int flags)
        {
            return new NameParts
            {
                UseFileSoftwareId = (flags & FileSoftwareId) != 0,
                UseFileTask = (flags & FileTask) != 0,
                UseFileTime = (flags & FileTime) != 0,
                UseFileDb = (flags & FileDb) != 0,
                UseDirSoftwareId = (flags & DirSoftwareId) != 0,
                UseDirKind = (flags & DirKind) != 0,
                UseDirInstance = (flags & DirInstance) != 0,
                UseDirTask = (flags & DirTask) != 0
            };
        }

        public int ToFlags()
        {
            var flags = 0;
            if (UseFileSoftwareId) flags |= FileSoftwareId;
            if (UseFileTask) flags |= FileTask;
            if (UseFileTime) flags |= FileTime;
            if (UseFileDb) flags |= FileDb;
            if (UseDirSoftwareId) flags |= DirSoftwareId;
            if (UseDirKind) flags |= DirKind;
            if (UseDirInstance) flags |= DirInstance;
            if (UseDirTask) flags |= DirTask;
            return flags;
        }
    }
}
