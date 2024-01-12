namespace LibSM64
{
    public static class SM64SoundBits
    {
        public static uint GetSoundBits(uint bank, uint playFlags, uint soundID, uint priority, uint flags2)
        {
            uint SOUND_STATUS_STARTING = 1;
            return (bank << 28) | (playFlags << 24) | (soundID << 16) | (priority << 8) | (flags2 << 4) | SOUND_STATUS_STARTING;
        }

        public static int MarioGameOver => (int)GetSoundBits(2, 4, 0x31, 0xFF, 8);
        public static int MarioHello => (int)GetSoundBits(2, 4, 0x32, 0xFF, 8);
        public static int MarioTwirlBounce => (int)GetSoundBits(2, 4, 0x34, 0x80, 8);
        public static int MarioHereWeGo => (int)GetSoundBits(2, 4, 0x0C, 0x80, 8);

        public static int GeneralCoin => (int)GetSoundBits(3, 8, 0x11, 0x80, 8);
        public static int GeneralCoinSpurt => (int)GetSoundBits(3, 0, 0x30, 0x00, 8);
        public static int GeneralCoinDrop => (int)GetSoundBits(3, 0, 0x36, 0x40, 8);

        public static int GeneralShortStar => (int)GetSoundBits(3, 0, 0x16, 0x00, 9);
        public static int GeneralStarAppears => (int)GetSoundBits(8, 0, 0x57, 0xFF, 9);

        public static int GeneralBoing1 => (int)GetSoundBits(3, 0, 0x6C, 0x40, 8);
        public static int GeneralBoing2 => (int)GetSoundBits(3, 0, 0x6D, 0x40, 8);
        public static int GeneralBoing3 => 0x3072;

        public static int GeneralWallExplosion => (int)GetSoundBits(3, 0, 0x0F, 0x00, 8);
        public static int GeneralBreakBox => (int)GetSoundBits(3, 0, 0x41, 0xC0, 8);

        public static int ActionHit1 => (int)GetSoundBits(0, 4, 0x44, 0xC0, 8);

    }
}
