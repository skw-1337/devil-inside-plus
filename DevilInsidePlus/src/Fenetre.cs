// Devil Inside Plus - fenetre principale : la tele du "Devil Setup" d'origine. Les images sont lues au demarrage
// dans les ressources de Devil Setup.exe (jamais redistribuees) ; les reglages s'affichent sur l'ecran cathodique.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace DevilInsidePlus
{
    /// <summary>Bitmaps du Devil Setup d'origine (ressources BITMAP de Devil Setup.exe).</summary>
    class Art : IDisposable
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr LoadLibraryEx(string f, IntPtr h, uint flags);
        [DllImport("kernel32.dll")] static extern bool FreeLibrary(IntPtr h);
        [DllImport("user32.dll")] static extern IntPtr LoadImage(IntPtr h, IntPtr nom, uint type, int cx, int cy, uint flags);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr h);

        // une bande de 14 images par bouton rotatif (eclairage propre a chacun) ; le gros bouton central = lueur de survol
        public Bitmap Tele, Masque, BoutonBig, BoutonVoix, BoutonGeneral, BoutonEffets, BoutonMusique, Lancer, Quitter, Haut, Bas, Off, On;
        public Bitmap[] L = new Bitmap[3], M = new Bitmap[3], H = new Bitmap[3];

        public static Art Charger(string setup)
        {
            if (!File.Exists(setup)) return null;
            IntPtr h = LoadLibraryEx(setup, IntPtr.Zero, 0x22); // LOAD_LIBRARY_AS_DATAFILE | AS_IMAGE_RESOURCE
            if (h == IntPtr.Zero) return null;
            try
            {
                Func<int, Bitmap> b = id =>
                {
                    IntPtr hb = LoadImage(h, (IntPtr)id, 0, 0, 0, 0x2000); // IMAGE_BITMAP, LR_CREATEDIBSECTION
                    if (hb == IntPtr.Zero) throw new InvalidOperationException("bitmap " + id);
                    try { return Image.FromHbitmap(hb); } finally { DeleteObject(hb); }
                };
                var a = new Art
                {
                    Tele = b(136), Masque = b(146), BoutonBig = b(188), BoutonVoix = b(180), BoutonGeneral = b(183), BoutonEffets = b(184), BoutonMusique = b(187),
                    Lancer = b(145), Quitter = b(138), Haut = b(176), Bas = b(177), Off = b(165), On = b(167)
                };
                for (int i = 0; i < 3; i++) { a.L[i] = Disque(b(162 + i), 1, 0.9f); a.M[i] = Disque(b(168 + i), 1, 0.9f); a.H[i] = Disque(b(172 + i), 1, 0.9f); }
                a.BoutonBig = Disque(a.BoutonBig, 14, 0.9f);
                a.BoutonVoix = Disque(a.BoutonVoix, 14, 0.88f); a.BoutonGeneral = Disque(a.BoutonGeneral, 14, 0.88f);
                a.BoutonEffets = Disque(a.BoutonEffets, 14, 0.88f); a.BoutonMusique = Disque(a.BoutonMusique, 14, 0.88f);
                a.Lancer = Disque(a.Lancer, 1, 0.9f); a.Quitter = Disque(a.Quitter, 1, 0.9f); a.On = Disque(a.On, 1, 0.9f); a.Off = Disque(a.Off, 1, 0.9f);
                return a;
            }
            catch (Exception ex) { Journal.Note("Images du Devil Setup illisibles : {0}", ex.Message); return null; }
            finally { FreeLibrary(h); }
        }

        /// <summary>Les sprites d'origine ont un fond carre : on ne garde qu'un disque a bord progressif (n images cote a cote).</summary>
        static Bitmap Disque(Bitmap src, int n, float k)
        {
            var dst = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(dst)) g.DrawImageUnscaled(src, 0, 0);
            src.Dispose();
            int w = dst.Width / n, h = dst.Height;
            var d = dst.LockBits(new Rectangle(0, 0, dst.Width, h), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            var px = new byte[d.Stride * h];
            Marshal.Copy(d.Scan0, px, 0, px.Length);
            float rayon = Math.Min(w, h) / 2f * k, flou = rayon * 0.14f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < dst.Width; x++)
                {
                    float dx = x % w + 0.5f - w / 2f, dy = y + 0.5f - h / 2f;
                    float a = Math.Max(0f, Math.Min(1f, (rayon - (float)Math.Sqrt(dx * dx + dy * dy)) / flou));
                    px[y * d.Stride + x * 4 + 3] = (byte)(a * 255);
                }
            Marshal.Copy(px, 0, d.Scan0, px.Length);
            dst.UnlockBits(d);
            return dst;
        }

        /// <summary>Accentuation (masque flou) apres l'agrandissement : la tele d'origine fait 484x480.</summary>
        public static void Accentuer(Bitmap bmp, float force)
        {
            var d = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppPArgb);
            int st = d.Stride, w = bmp.Width, h = bmp.Height;
            var src = new byte[st * h]; Marshal.Copy(d.Scan0, src, 0, src.Length);
            var dst = (byte[])src.Clone();
            for (int y = 1; y < h - 1; y++)
                for (int x = 1; x < w - 1; x++)
                {
                    int o = y * st + x * 4;
                    for (int c = 0; c < 3; c++)
                    {
                        int somme = 0;
                        for (int j = -1; j <= 1; j++) for (int i = -1; i <= 1; i++) somme += src[o + j * st + i * 4 + c];
                        float v = src[o + c] + force * (src[o + c] - somme / 9f);
                        dst[o + c] = (byte)Math.Max(0, Math.Min(src[o + 3], v));
                    }
                }
            Marshal.Copy(dst, 0, d.Scan0, dst.Length);
            bmp.UnlockBits(d);
        }

        public void Dispose()
        {
            foreach (var x in new[] { Tele, Masque, BoutonBig, BoutonVoix, BoutonGeneral, BoutonEffets, BoutonMusique, Lancer, Quitter, Haut, Bas, Off, On }.Concat(L).Concat(M).Concat(H))
                if (x != null) x.Dispose();
        }
    }

    class Fenetre : Form
    {
        readonly Jeu jeu;
        Reglages r;
        Process partie;
        readonly Art art;

        // coordonnees dans l'image d'origine (484x480), mises a l'echelle par S
        const int LargeurTele = 484, HauteurTele = 480;
        float S = 1.6f;
        Bitmap fond;          // tele mise a l'echelle

        enum Zone { Aucune, Voix, General, Effets, Musique, H, M, L, Ecran, Haut, Bas, Page, Lancer, Quitter, Ligne }
        static readonly Dictionary<Zone, RectangleF> Zones = new Dictionary<Zone, RectangleF>
        {
            { Zone.Voix, new RectangleF(36, 48, 37, 37) },
            { Zone.General, new RectangleF(426, 64, 37, 37) },
            { Zone.Effets, new RectangleF(423, 109, 37, 37) },
            { Zone.Musique, new RectangleF(419, 154, 37, 37) },
            { Zone.H, new RectangleF(43, 109, 24, 24) },
            { Zone.M, new RectangleF(47, 149, 24, 24) },
            { Zone.L, new RectangleF(51, 189, 23, 24) },
            { Zone.Ecran, new RectangleF(419, 201, 16, 16) },
            { Zone.Haut, new RectangleF(412, 255, 19, 12) },
            { Zone.Bas, new RectangleF(413, 273, 17, 12) },
            { Zone.Page, new RectangleF(224, 278, 63, 67) },
            { Zone.Lancer, new RectangleF(350, 294, 45, 49) },
            { Zone.Quitter, new RectangleF(116, 317, 38, 38) },
        };

        Zone survol = Zone.Aucune, enfonce = Zone.Aucune;
        int ligneSurvol = -1, selection = 0, page = 0;
        string message = "";
        DateTime messageJusqua;
        readonly System.Windows.Forms.Timer horloge = new System.Windows.Forms.Timer { Interval = 15 };
        readonly Stopwatch chrono = Stopwatch.StartNew();
        Bitmap couche;        // contenu de l'ecran (texte, lignes) dessine une seule fois par changement

        // ------------------------------------------------------------------ options affichees sur l'ecran

        class Option
        {
            public string Libelle; public Func<string> Valeur; public Action<int> Changer; public string Aide;
        }
        readonly List<string> titres = new List<string>();
        readonly List<List<Option>> pages = new List<List<Option>>();

        static readonly string[] Resolutions = { "auto", "1280x720", "1600x900", "1920x1080", "2560x1440", "3440x1440", "3840x2160", "1024x768", "1280x960", "1600x1200", "1920x1440" };
        static readonly string[] Niveaux = { "LOW", "MEDIUM", "HIGH" }, NiveauxTexte = { "Low", "Medium", "High" };
        static readonly int[] Fps = { 30, 60, 75, 90, 120, 144, 165, 240 };

        static T Suivant<T>(T[] t, T v, int sens) { int i = Array.IndexOf(t, v); if (i < 0) i = 0; return t[((i + sens) % t.Length + t.Length) % t.Length]; }
        static string OuiNon(bool b) { return b ? "On" : "Off"; }
        static int Pas(int v, int sens, int pas, int min, int max) { return Math.Max(min, Math.Min(max, (v / pas) * pas + sens * pas)); }

        void Construire()
        {
            Func<string> natif = () => { var b = Screen.PrimaryScreen.Bounds; return b.Width + "x" + b.Height; };
            Page("DISPLAY",
                new Option { Libelle = "Resolution", Valeur = () => r.Resolution == "auto" ? "Native " + natif() : r.Resolution, Changer = s => r.Resolution = Suivant(Resolutions, r.Resolution, s), Aide = "The engine renders natively at any resolution, 16:9 included." },
                new Option { Libelle = "Window mode", Valeur = () => r.PleinEcran ? "Fullscreen" : "Borderless", Changer = s => r.PleinEcran = !r.PleinEcran, Aide = "Borderless: instant Alt+Tab." },
                new Option { Libelle = "Render scale", Valeur = () => r.Surechantillonnage > 1 ? "SSAA " + r.Surechantillonnage + "x" : "Native", Changer = s => r.Surechantillonnage = (r.Surechantillonnage - 1 + s + 3) % 3 + 1 },
                new Option { Libelle = "Anti-aliasing", Valeur = () => r.Antialiasing, Changer = s => r.Antialiasing = Suivant(new[] { "off", "2x", "4x", "8x" }, r.Antialiasing, s) },
                new Option { Libelle = "Texture filtering", Valeur = () => r.Anisotrope > 1 ? r.Anisotrope + "x" : "Game", Changer = s => r.Anisotrope = Suivant(new[] { 0, 2, 4, 8, 16 }, r.Anisotrope, s) },
                new Option { Libelle = "Frame rate cap", Valeur = () => r.FpsMax.ToString(), Changer = s => r.FpsMax = Suivant(Fps, r.FpsMax, s), Aide = "60 is the safe value for this engine." },
                new Option { Libelle = "V-Sync", Valeur = () => OuiNon(r.VSync), Changer = s => r.VSync = !r.VSync },
                new Option { Libelle = "Field of view", Valeur = () => r.PatchFov ? string.Format("{0:0}°{1}", ChampH(), r.FovBonus > 0 ? " +" + r.FovBonus + "%" : "") : "76° original", Changer = s => r.FovBonus = Pas(r.FovBonus, s, 5, 0, 40), Aide = "Hor+: 76° in 4:3, wider on widescreen." },
                new Option { Libelle = "Mipmaps", Valeur = () => OuiNon(r.MipmapsAuto), Changer = s => r.MipmapsAuto = !r.MipmapsAuto });
            Page("DETAILS",
                new Option { Libelle = "Lighting", Valeur = () => Niveau(r.Eclairage), Changer = s => r.Eclairage = Suivant(Niveaux, r.Eclairage, s) },
                new Option { Libelle = "Shadows", Valeur = () => Niveau(r.Ombres), Changer = s => r.Ombres = Suivant(Niveaux, r.Ombres, s) },
                new Option { Libelle = "Textures", Valeur = () => Niveau(r.Textures), Changer = s => r.Textures = Suivant(Niveaux, r.Textures, s) },
                new Option { Libelle = "Mirrors", Valeur = () => OuiNon(r.Miroirs), Changer = s => r.Miroirs = !r.Miroirs },
                new Option { Libelle = "Floating cameras", Valeur = () => OuiNon(r.CamerasFlottantes), Changer = s => r.CamerasFlottantes = !r.CamerasFlottantes },
                new Option { Libelle = "Slow motion", Valeur = () => OuiNon(r.Ralenti), Changer = s => r.Ralenti = !r.Ralenti },
                new Option { Libelle = "Subtitles", Valeur = () => OuiNon(r.Soustitres), Changer = s => r.Soustitres = !r.Soustitres });
            Page("SOUND",
                new Option { Libelle = "Master volume", Valeur = () => r.VolumeGeneral + " %", Changer = s => r.VolumeGeneral = Pas(r.VolumeGeneral, s, 10, 0, 100), Aide = "Also on the top right knob." },
                new Option { Libelle = "Effects", Valeur = () => r.VolumeEffets + " %", Changer = s => r.VolumeEffets = Pas(r.VolumeEffets, s, 10, 0, 100) },
                new Option { Libelle = "Music (CD)", Valeur = () => r.VolumeMusique + " %", Changer = s => r.VolumeMusique = Pas(r.VolumeMusique, s, 10, 0, 100) },
                new Option { Libelle = "Voices", Valeur = () => r.VolumeVoix + " %", Changer = s => r.VolumeVoix = Pas(r.VolumeVoix, s, 10, 0, 100), Aide = "Also on the top left knob." },
                new Option { Libelle = "EAX (3D sound)", Valeur = () => OuiNon(r.Eax), Changer = s => r.Eax = !r.Eax });
            var moteur = new List<Option>();
            foreach (var c in Jeu.Correctifs)
            {
                var cc = c;
                moteur.Add(new Option { Libelle = Court(c.Id), Valeur = () => OuiNon(Actif(cc)), Changer = s => Basculer(cc), Aide = c.Description });
            }
            moteur.Add(new Option { Libelle = "Log file", Valeur = () => "Open", Changer = s => { try { Process.Start("notepad.exe", "\"" + Journal.Fichier + "\""); } catch { } } });
            moteur.Add(new Option { Libelle = "Game folder", Valeur = () => "Open", Changer = s => Process.Start("explorer.exe", "\"" + jeu.Dossier + "\"") });
            moteur.Add(new Option { Libelle = "Original Devil.exe", Valeur = () => "Restore", Changer = s => Restaurer() });
            Page("ENGINE", moteur.ToArray());
        }

        void Page(string titre, params Option[] o) { titres.Add(titre); pages.Add(o.ToList()); }

        static string Court(string id)
        {
            switch (id)
            {
                case "horloge": return "Engine clock (speed)";
                case "cd": return "No CD";
                case "musique": return "Music from .ogg";
                case "fov": return "Widescreen FOV";
                case "surfaces": return "Alt+Tab (video)";
                case "entrees": return "Alt+Tab (input)";
                default: return id;
            }
        }
        bool Actif(Correctif c) { return jeu.Actif(r, c); }
        void Basculer(Correctif c)
        {
            switch (c.Id)
            {
                case "horloge": r.PatchHorloge = !r.PatchHorloge; break;
                case "cd": r.PatchCD = !r.PatchCD; break;
                case "musique": r.PatchMusique = !r.PatchMusique; break;
                case "fov": r.PatchFov = !r.PatchFov; break;
                case "surfaces": r.PatchSurfaces = !r.PatchSurfaces; break;
                case "entrees": r.PatchEntrees = !r.PatchEntrees; break;
            }
        }
        static string Niveau(string s) { int i = Array.IndexOf(Niveaux, (s ?? "").ToUpperInvariant()); return NiveauxTexte[i < 0 ? 2 : i]; }
        double ChampH() { return 2 * Math.Atan(Math.Tan(1.3265 / 2) / Jeu.FacteurFov(r)) * 180 / Math.PI; }

        void Prereglage(Zone z)
        {
            if (z == Zone.H) { r.Resolution = "auto"; r.Antialiasing = "8x"; r.Anisotrope = 16; r.MipmapsAuto = true; r.Eclairage = r.Ombres = r.Textures = "HIGH"; r.Miroirs = true; Dire("High: native resolution, 8x AA, 16x filtering."); }
            if (z == Zone.M) { r.Resolution = "1920x1080"; r.Surechantillonnage = 1; r.Antialiasing = "4x"; r.Anisotrope = 8; r.MipmapsAuto = true; r.Eclairage = r.Ombres = r.Textures = "HIGH"; Dire("Medium: 1920x1080, 4x AA, 8x filtering."); }
            if (z == Zone.L) { r.Resolution = "1024x768"; r.Surechantillonnage = 1; r.Antialiasing = "off"; r.Anisotrope = 0; r.MipmapsAuto = false; Dire("Low: the original 1024x768 4:3 look from 2000."); }
            Enregistrer();
        }

        // ------------------------------------------------------------------ construction de la fenetre

        public Fenetre(Jeu jeu, bool direct)
        {
            this.jeu = jeu;
            r = Reglages.Charger(jeu.FichierReglages);
            art = Art.Charger(Path.Combine(jeu.Dossier, "Devil Setup.exe"));
            Text = "Devil Inside Plus";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = true;
            BackColor = Color.Black;
            KeyPreview = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            // echelle : la tele occupe environ 60 % de la hauteur utile de l'ecran
            S = Math.Max(1f, Screen.PrimaryScreen.WorkingArea.Height * 0.54f / HauteurTele);
            ClientSize = new Size((int)(LargeurTele * S), (int)(HauteurTele * S));
            if (art != null)
            {
                fond = new Bitmap(ClientSize.Width, ClientSize.Height, PixelFormat.Format32bppPArgb);
                using (var g = Graphics.FromImage(fond)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality; g.DrawImage(art.Tele, 0, 0, ClientSize.Width, ClientSize.Height); }
                Art.Accentuer(fond, 0.9f);
                Detourer(fond, art.Masque);
            }
            else Journal.Note("Devil Setup.exe absent : affichage simplifie");

            Construire();
            horloge.Tick += (s, e) =>
            {
                if (DateTime.Now > messageJusqua && message != "") message = "";
                Afficher();
            };
            horloge.Start();
            Shown += (s, e) => Afficher();
            if (direct) Shown += (s, e) => Jouer();
        }

        /// <summary>Transparence par pixel d'apres le masque (magenta = exterieur) agrandi en bicubique : bord lisse, sans escalier.</summary>
        void Detourer(Bitmap bmp, Bitmap masque)
        {
            int w = bmp.Width, h = bmp.Height;
            using (var bin = new Bitmap(masque.Width, masque.Height, PixelFormat.Format32bppArgb))
            using (var grand = new Bitmap(w, h, PixelFormat.Format32bppArgb))
            {
                for (int y = 0; y < masque.Height; y++)
                    for (int x = 0; x < masque.Width; x++)
                        bin.SetPixel(x, y, Exterieur(masque.GetPixel(x, y)) ? Color.Black : Color.White);
                using (var g = Graphics.FromImage(grand))
                using (var att = new ImageAttributes())
                {
                    att.SetWrapMode(WrapMode.TileFlipXY); // pas de bord noir parasite a l'agrandissement
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(bin, new Rectangle(0, 0, w, h), 0, 0, bin.Width, bin.Height, GraphicsUnit.Pixel, att);
                }
                var dm = grand.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                var db = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadWrite, PixelFormat.Format32bppPArgb);
                var pm = new byte[dm.Stride * h]; var pb = new byte[db.Stride * h];
                Marshal.Copy(dm.Scan0, pm, 0, pm.Length); Marshal.Copy(db.Scan0, pb, 0, pb.Length);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float k = pm[y * dm.Stride + x * 4 + 1] / 255f;
                        int o = y * db.Stride + x * 4;
                        for (int c = 0; c < 4; c++) pb[o + c] = (byte)(pb[o + c] * k);
                    }
                Marshal.Copy(pb, 0, db.Scan0, pb.Length);
                grand.UnlockBits(dm); bmp.UnlockBits(db);
            }
        }
        static bool Exterieur(Color c) { return c.R > 200 && c.G < 80 && c.B > 200; }

        RectangleF Px(RectangleF t) { return new RectangleF(t.X * S, t.Y * S, t.Width * S, t.Height * S); }

        // ------------------------------------------------------------------ dessin

        // ------------------------------------------------------------------ fenetre "layered" : alpha par pixel, rendu dans une surface fixe
        [StructLayout(LayoutKind.Sequential)] struct TAILLE { public int cx, cy; public TAILLE(int x, int y) { cx = x; cy = y; } }
        [StructLayout(LayoutKind.Sequential)] struct POINT { public int x, y; }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] struct MELANGE { public byte Op, Flags, Alpha, Format; }
        [StructLayout(LayoutKind.Sequential)] struct BITMAPINFOHEADER { public int biSize, biWidth, biHeight; public short biPlanes, biBitCount; public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant; }
        [DllImport("user32.dll")] static extern bool UpdateLayeredWindow(IntPtr h, IntPtr dcDst, IntPtr ptDst, ref TAILLE taille, IntPtr dcSrc, ref POINT ptSrc, int cle, ref MELANGE m, int flags);
        [DllImport("gdi32.dll")] static extern IntPtr CreateDIBSection(IntPtr dc, ref BITMAPINFOHEADER bi, uint usage, out IntPtr bits, IntPtr sec, uint off);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr o);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr o);

        protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ExStyle |= 0x80000; return cp; } } // WS_EX_LAYERED

        IntPtr dcSurface, dibSurface; Bitmap surface;

        // le rendu se fait a chaque tic : les demandes de rafraichissement classiques deviennent inutiles
        new void Invalidate() { }
        new void Invalidate(Rectangle r) { }

        void Afficher()
        {
            if (!IsHandleCreated || WindowState == FormWindowState.Minimized) return; // rien a dessiner pendant la partie
            int w = ClientSize.Width, h = ClientSize.Height;
            if (surface == null)
            {
                var bi = new BITMAPINFOHEADER { biSize = 40, biWidth = w, biHeight = -h, biPlanes = 1, biBitCount = 32 };
                IntPtr bits;
                dcSurface = CreateCompatibleDC(IntPtr.Zero);
                dibSurface = CreateDIBSection(dcSurface, ref bi, 0, out bits, IntPtr.Zero, 0);
                SelectObject(dcSurface, dibSurface);
                surface = new Bitmap(w, h, w * 4, PixelFormat.Format32bppPArgb, bits);
            }
            using (var g = Graphics.FromImage(surface)) Rendre(g);
            var taille = new TAILLE(w, h); var src = new POINT(); var m = new MELANGE { Op = 0, Alpha = 255, Format = 1 };
            UpdateLayeredWindow(Handle, IntPtr.Zero, IntPtr.Zero, ref taille, dcSurface, ref src, 0, ref m, 2);
        }

        Bitmap socle; string sigSocle;   // tele + boutons, recalcules seulement quand un bouton change

        /// <summary>Rendu du lanceur dans un PNG (pour la presentation du depot), sans afficher de fenetre.</summary>
        public void Capturer(string fichier, int pageVoulue)
        {
            page = Math.Max(0, Math.Min(pages.Count - 1, pageVoulue));
            using (var bmp = new Bitmap(ClientSize.Width, ClientSize.Height, PixelFormat.Format32bppPArgb))
            {
                using (var g = Graphics.FromImage(bmp)) Rendre(g);
                bmp.Save(fichier, ImageFormat.Png);
            }
        }

        void Rendre(Graphics g)
        {
            double lueur = survol == Zone.Page ? Math.Min(1.0, (DateTime.Now - survolPage).TotalSeconds / 0.3) : 0;
            string sig = string.Join("|", r.VolumeVoix, r.VolumeGeneral, r.VolumeEffets, r.VolumeMusique, (int)(lueur * 13), survol, enfonce, r.PleinEcran, partie != null);
            if (socle == null || sig != sigSocle)
            {
                if (socle == null) socle = new Bitmap(ClientSize.Width, ClientSize.Height, PixelFormat.Format32bppPArgb);
                using (var gs = Graphics.FromImage(socle))
                {
                    gs.Clear(Color.Transparent);
                    gs.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    gs.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    gs.SmoothingMode = SmoothingMode.AntiAlias;
                    if (fond != null) gs.DrawImageUnscaled(fond, 0, 0);
                    else using (var b = new LinearGradientBrush(ClientRectangle, Color.FromArgb(70, 42, 24), Color.FromArgb(30, 18, 10), 90f)) gs.FillRectangle(b, ClientRectangle);
                    if (art != null)
                    {
                        Bouton(gs, Zone.Voix, art.BoutonVoix, 14, r.VolumeVoix);
                        Bouton(gs, Zone.General, art.BoutonGeneral, 14, r.VolumeGeneral);
                        Bouton(gs, Zone.Effets, art.BoutonEffets, 14, r.VolumeEffets);
                        Bouton(gs, Zone.Musique, art.BoutonMusique, 14, r.VolumeMusique);
                        // gros bouton : sa lueur s'allume en 0,3 s au survol
                        Bouton(gs, Zone.Page, art.BoutonBig, 14, (int)(lueur * 100));
                        Etat(gs, Zone.H, art.H); Etat(gs, Zone.M, art.M); Etat(gs, Zone.L, art.L);
                        Sprite(gs, Zone.Ecran, r.PleinEcran ? art.On : art.Off);
                        if (survol == Zone.Haut) Sprite(gs, Zone.Haut, art.Haut);
                        if (survol == Zone.Bas) Sprite(gs, Zone.Bas, art.Bas);
                        if (survol == Zone.Lancer || partie != null) Sprite(gs, Zone.Lancer, art.Lancer);
                        if (survol == Zone.Quitter) Sprite(gs, Zone.Quitter, art.Quitter);
                    }
                }
                sigSocle = sig;
            }
            g.CompositingMode = CompositingMode.SourceCopy;
            g.DrawImageUnscaled(socle, 0, 0);
            g.CompositingMode = CompositingMode.SourceOver;
            DessinerEcran(g);
        }

        void Sprite(Graphics g, Zone z, Bitmap b)
        {
            var c = Zones[z]; float cx = c.X + c.Width / 2, cy = c.Y + c.Height / 2;
            g.DrawImage(b, Px(new RectangleF(cx - b.Width / 2f, cy - b.Height / 2f, b.Width, b.Height)));
        }

        void Etat(Graphics g, Zone z, Bitmap[] b)
        {
            if (enfonce == z) Sprite(g, z, b[2]);
            else if (survol == z) Sprite(g, z, b[1]);
        }

        /// <summary>Bouton rotatif : bande de N images, l'image choisie d'apres une valeur 0-100.</summary>
        void Bouton(Graphics g, Zone z, Bitmap bande, int n, int valeur)
        {
            int w = bande.Width / n, i = Math.Max(0, Math.Min(n - 1, (int)Math.Round(valeur / 100.0 * (n - 1))));
            var c = Zones[z]; float cx = c.X + c.Width / 2, cy = c.Y + c.Height / 2;
            g.DrawImage(bande, Px(new RectangleF(cx - w / 2f, cy - bande.Height / 2f, w, bande.Height)), new RectangleF(i * w, 0, w, bande.Height), GraphicsUnit.Pixel);
        }

        // ecran cathodique : texte phosphore ambre net, lignes de balayage, bande lumineuse qui descend
        static readonly Color Phosphore = Color.FromArgb(240, 186, 116), PhosphoreVif = Color.FromArgb(255, 238, 205), PhosphoreSombre = Color.FromArgb(214, 184, 140);
        // verre visible (coins tres arrondis) et zone de texte sure, en coordonnees d'origine
        static readonly RectangleF Verre = new RectangleF(122, 75, 250, 193);
        const float TexteX = 147, TexteL = 202, OngletsY = 90, LignesY = 107, LigneT = 12.8f, PiedY = 230;

        float LigneH { get { return LigneT * S; } }
        Rectangle EcranPx() { return Rectangle.Round(Px(Verre)); }

        Font Police(float taille, FontStyle style, string famille = "Segoe UI") { return new Font(famille, taille * S, style, GraphicsUnit.Pixel); }

        string signature;
        string Signature()
        {
            return page + "|" + selection + "|" + ligneSurvol + "|" + survol + "|" + (DateTime.Now < messageJusqua ? message : "") + "|" +
                string.Join(";", pages[page].Select(o => o.Valeur()).ToArray());
        }

        void DessinerEcran(Graphics g)
        {
            var e = EcranPx();
            string sig = Signature();
            if (couche == null || sig != signature)
            {
                if (couche == null) couche = new Bitmap(e.Width, e.Height, PixelFormat.Format32bppPArgb);
                using (var gc = Graphics.FromImage(couche))
                {
                    gc.Clear(Color.Transparent);
                    gc.TranslateTransform(-e.X, -e.Y);
                    DessinerContenu(gc);
                }
                Masquer(couche, 0);
                signature = sig;
            }
            g.DrawImageUnscaled(couche, e.X, e.Y);
            // bande lumineuse : seule partie animee, masquee par le verre comme le reste
            var b = Bande();
            int y0 = Math.Max(b.Y, e.Y), y1 = Math.Min(b.Bottom, e.Bottom);
            if (y1 <= y0) return;
            if (bande == null || bande.Height != b.Height) { if (bande != null) bande.Dispose(); bande = new Bitmap(e.Width, b.Height, PixelFormat.Format32bppPArgb); }
            using (var gb = Graphics.FromImage(bande))
            using (var br = new LinearGradientBrush(new Rectangle(0, 0, e.Width, b.Height + 1), Color.FromArgb(0, 255, 200, 150), Color.FromArgb(24, 255, 210, 160), 90f))
            { gb.Clear(Color.Transparent); gb.FillRectangle(br, 0, 0, e.Width, b.Height); }
            Masquer(bande, b.Y - e.Y);
            g.DrawImageUnscaled(bande, e.X, b.Y);
        }
        Bitmap bande;

        // masque du verre : 1 sur le verre sombre, 0 sur le cadre beige, avec une transition douce.
        // Calcule depuis l'image de la tele elle-meme, pour suivre exactement sa forme bombee.
        float[] masqueVerre;
        void CalculerMasque()
        {
            var e = EcranPx();
            int w = e.Width, h = e.Height;
            var m = new float[w * h];
            if (fond != null)
            {
                var d = fond.LockBits(e, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
                var px = new byte[d.Stride * h]; Marshal.Copy(d.Scan0, px, 0, px.Length); fond.UnlockBits(d);
                // cadre = pixels clairs relies au bord exterieur (remplissage depuis les bords) ;
                // le reflet clair a l'interieur du verre n'y est pas relie et reste donc "verre"
                var clair = new bool[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int o = y * d.Stride + x * 4;
                        clair[y * w + x] = 0.11f * px[o] + 0.59f * px[o + 1] + 0.3f * px[o + 2] >= 70f;
                    }
                var cadre = new bool[w * h];
                var file = new Queue<int>();
                for (int x = 0; x < w; x++) { file.Enqueue(x); file.Enqueue((h - 1) * w + x); }
                for (int y = 0; y < h; y++) { file.Enqueue(y * w); file.Enqueue(y * w + w - 1); }
                while (file.Count > 0)
                {
                    int i = file.Dequeue();
                    if (cadre[i] || !clair[i]) continue;
                    cadre[i] = true;
                    int x = i % w, y = i / w;
                    if (x > 0) file.Enqueue(i - 1); if (x < w - 1) file.Enqueue(i + 1);
                    if (y > 0) file.Enqueue(i - w); if (y < h - 1) file.Enqueue(i + w);
                }
                for (int i = 0; i < m.Length; i++) m[i] = cadre[i] ? 0f : 1f;
                // adoucit le bord (deux flous de boite separables)
                int r = Math.Max(2, (int)(3 * S));
                for (int passe = 0; passe < 2; passe++) { m = Flou(m, w, h, r, true); m = Flou(m, w, h, r, false); }
            }
            else for (int i = 0; i < m.Length; i++) m[i] = 1f;
            masqueVerre = m;
        }

        static float[] Flou(float[] m, int w, int h, int r, bool horizontal)
        {
            var o = new float[m.Length];
            int n = horizontal ? w : h, lignes = horizontal ? h : w;
            for (int l = 0; l < lignes; l++)
            {
                float somme = 0; int cpt = 0;
                Func<int, int> idx = i => horizontal ? l * w + i : i * w + l;
                for (int i = 0; i < Math.Min(r, n); i++) { somme += m[idx(i)]; cpt++; }
                for (int i = 0; i < n; i++)
                {
                    if (i + r < n) { somme += m[idx(i + r)]; cpt++; }
                    if (i - r - 1 >= 0) { somme -= m[idx(i - r - 1)]; cpt--; }
                    o[idx(i)] = somme / cpt;
                }
            }
            return o;
        }

        /// <summary>Multiplie une couche (premultipliee) par le masque du verre ; dy = decalage vertical dans l'ecran.</summary>
        void Masquer(Bitmap bmp, int dy)
        {
            if (masqueVerre == null) CalculerMasque();
            int w = EcranPx().Width, hm = masqueVerre.Length / w;
            var d = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppPArgb);
            var px = new byte[d.Stride * bmp.Height]; Marshal.Copy(d.Scan0, px, 0, px.Length);
            for (int y = 0; y < bmp.Height; y++)
            {
                int ym = y + dy;
                for (int x = 0; x < Math.Min(w, bmp.Width); x++)
                {
                    float k = ym < 0 || ym >= hm ? 0f : masqueVerre[ym * w + x];
                    int o = y * d.Stride + x * 4;
                    px[o] = (byte)(px[o] * k); px[o + 1] = (byte)(px[o + 1] * k); px[o + 2] = (byte)(px[o + 2] * k); px[o + 3] = (byte)(px[o + 3] * k);
                }
            }
            Marshal.Copy(px, 0, d.Scan0, px.Length);
            bmp.UnlockBits(d);
        }

        /// <summary>Position de la bande d'apres l'horloge : fluide quel que soit le rythme du minuteur.</summary>
        Rectangle Bande()
        {
            var v = Px(Verre);
            float t = (float)(chrono.Elapsed.TotalSeconds / 4.0 % 1.0), h = 22 * S;
            float y = v.Top - h + t * (v.Height + h);
            return Rectangle.Round(new RectangleF(v.X, y, v.Width, h));
        }

        void DessinerContenu(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            var v = Px(Verre);
            // lueur du tube
            using (var p = new GraphicsPath())
            {
                p.AddEllipse(RectangleF.Inflate(v, 10 * S, 10 * S));
                using (var b = new PathGradientBrush(p) { CenterColor = Color.FromArgb(46, 255, 150, 70), SurroundColors = new[] { Color.FromArgb(0, 0, 0, 0) } }) g.FillPath(b, p);
            }
            float x0 = TexteX * S, x1 = (TexteX + TexteL) * S;
            using (var fOnglet = Police(7.6f, FontStyle.Bold, "Georgia"))
            using (var fLib = Police(7.6f, FontStyle.Regular))
            using (var fVal = Police(7.6f, FontStyle.Bold))
            using (var fPied = Police(7.2f, FontStyle.Regular))
            using (var trait = new Pen(Color.FromArgb(110, 240, 186, 116), Math.Max(1f, 0.6f * S)))
            {
                // onglets, repartis sur toute la largeur
                float largeur = (x1 - x0) / titres.Count;
                for (int i = 0; i < titres.Count; i++)
                {
                    var r0 = new RectangleF(x0 + i * largeur, OngletsY * S, largeur, 12 * S);
                    Texte(g, titres[i], fOnglet, r0, i == page ? PhosphoreVif : PhosphoreSombre, StringAlignment.Center);
                    if (i == page) using (var b = new SolidBrush(Phosphore)) g.FillRectangle(b, r0.X + largeur * 0.2f, r0.Bottom + 1 * S, largeur * 0.6f, Math.Max(1.5f, 1.1f * S));
                }
                g.DrawLine(trait, x0, (LignesY - 3) * S, x1, (LignesY - 3) * S);
                // lignes
                var opts = pages[page];
                for (int i = 0; i < opts.Count; i++)
                {
                    var rl = new RectangleF(x0, (LignesY + i * LigneT) * S, x1 - x0, LigneT * S);
                    bool sel = i == selection, sur = i == ligneSurvol;
                    if (sel || sur) using (var b = new SolidBrush(Color.FromArgb(sel ? 44 : 22, 255, 176, 90))) g.FillRectangle(b, RectangleF.Inflate(rl, 3 * S, -0.5f * S));
                    var c = sel ? PhosphoreVif : Phosphore;
                    Texte(g, opts[i].Libelle, fLib, rl, c, StringAlignment.Near);
                    Texte(g, opts[i].Valeur(), fVal, rl, c, StringAlignment.Far);
                }
                // pied : filet puis aide centree sur deux lignes au plus
                g.DrawLine(trait, x0, (PiedY - 3) * S, x1, (PiedY - 3) * S);
                bool msg = DateTime.Now < messageJusqua;
                using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near, Trimming = StringTrimming.EllipsisWord })
                using (var b = new SolidBrush(msg ? PhosphoreVif : PhosphoreSombre))
                    g.DrawString(msg ? message : AideCourante(), fPied, b, new RectangleF(x0 + 4 * S, (PiedY - 0.5f) * S, x1 - x0 - 8 * S, 20 * S), fmt);
            }
            // lignes de balayage fixes
            using (var b = new SolidBrush(Color.FromArgb(34, 0, 0, 0)))
                for (float y = v.Top; y < v.Bottom; y += Math.Max(2f, S * 1.5f)) g.FillRectangle(b, v.X, y, v.Width, Math.Max(1f, S * 0.5f));
        }

        static void Texte(Graphics g, string t, Font f, RectangleF r, Color c, StringAlignment al)
        {
            using (var fmt = new StringFormat { Alignment = al, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            using (var b = new SolidBrush(c)) g.DrawString(t, f, b, r, fmt);
        }

        string AideCourante()
        {
            switch (survol)
            {
                case Zone.Voix: return "Voices " + r.VolumeVoix + " % · turn it, scroll, or click left/right side.";
                case Zone.General: return "Master " + r.VolumeGeneral + " % · turn it, scroll, or click left/right side.";
                case Zone.Effets: return "Effects " + r.VolumeEffets + " % · turn it, scroll, or click left/right side.";
                case Zone.Musique: return "Music " + r.VolumeMusique + " % · turn it, scroll, or click left/right side.";
                case Zone.H: return "H: high quality (native resolution, 8x AA).";
                case Zone.M: return "M: medium quality (1920x1080, 4x AA).";
                case Zone.L: return "L: the original game (1024x768, 4:3).";
                case Zone.Ecran: return "Fullscreen / borderless window.";
                case Zone.Page: return "Change page.";
                case Zone.Haut: case Zone.Bas: return "Select a line.";
                case Zone.Lancer: return partie != null ? "The game is running." : "Launch the game.";
                case Zone.Quitter: return "Quit.";
            }
            var opts = pages[page]; int i = ligneSurvol >= 0 ? ligneSurvol : selection;
            if (i >= 0 && i < opts.Count && opts[i].Aide != null) return opts[i].Aide;
            return "Click: next value · right-click: previous · drag the wood to move";
        }

        static GraphicsPath Arrondi(RectangleF r, float rayon)
        {
            var p = new GraphicsPath(); float d = rayon * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure(); return p;
        }

        // ------------------------------------------------------------------ souris et clavier

        Zone Toucher(Point p, out int ligne)
        {
            ligne = -1;
            foreach (var kv in Zones) if (Px(kv.Value).Contains(p)) return kv.Key;
            float x0 = TexteX * S, x1 = (TexteX + TexteL) * S, y0 = LignesY * S;
            if (p.X >= x0 - 4 * S && p.X <= x1 + 4 * S && p.Y >= y0)
            {
                int i = (int)((p.Y - y0) / LigneH);
                if (i < pages[page].Count) { ligne = i; return Zone.Ligne; }
            }
            // onglets en haut de l'ecran
            if (p.Y >= OngletsY * S && p.Y < y0 && p.X >= x0 && p.X <= x1) return Zone.Page;
            return Zone.Aucune;
        }

        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int m, IntPtr w, IntPtr l);

        // boutons de volume : glisser vers le haut/bas pour tourner, ou clic a gauche (-) / a droite (+)
        static bool EstVolume(Zone z) { return z == Zone.Voix || z == Zone.General || z == Zone.Effets || z == Zone.Musique; }
        int Volume(Zone z) { return z == Zone.Voix ? r.VolumeVoix : z == Zone.General ? r.VolumeGeneral : z == Zone.Effets ? r.VolumeEffets : r.VolumeMusique; }
        void Volume(Zone z, int v)
        {
            v = Math.Max(0, Math.Min(100, v));
            if (z == Zone.Voix) r.VolumeVoix = v; else if (z == Zone.General) r.VolumeGeneral = v; else if (z == Zone.Effets) r.VolumeEffets = v; else r.VolumeMusique = v;
            Dire((z == Zone.Voix ? "Voices" : z == Zone.General ? "Master volume" : z == Zone.Effets ? "Effects" : "Music") + ": " + v + " %");
        }
        DateTime survolPage;
        Zone tourne = Zone.Aucune; bool aTourne; double angleAvant, volFlottant; Point pDepart;

        double Angle(Zone z, Point p)
        {
            var c = Px(Zones[z]);
            return Math.Atan2(p.X - (c.X + c.Width / 2), (c.Y + c.Height / 2) - p.Y) * 180 / Math.PI;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (tourne != Zone.Aucune)
            {
                // rotation autour du centre du bouton : un tour de 270 degres = 0 a 100 %
                if (Math.Abs(e.X - pDepart.X) + Math.Abs(e.Y - pDepart.Y) > 3 * S) aTourne = true;
                double a = Angle(tourne, e.Location), da = a - angleAvant;
                if (da > 180) da -= 360; else if (da < -180) da += 360;
                angleAvant = a;
                if (aTourne)
                {
                    volFlottant = Math.Max(0, Math.Min(100, volFlottant + da / 270.0 * 100));
                    int v = (int)Math.Round(volFlottant);
                    if (v != Volume(tourne)) { Volume(tourne, v); Invalidate(Rectangle.Round(RectangleF.Inflate(Px(Zones[tourne]), 4 * S, 4 * S))); }
                }
                return;
            }
            int l; var z = Toucher(e.Location, out l);
            if (z != survol || l != ligneSurvol) { if (z == Zone.Page && survol != Zone.Page) survolPage = DateTime.Now; survol = z; ligneSurvol = l; Invalidate(); }
            Cursor = z == Zone.Aucune ? Cursors.Default : Cursors.Hand;
            base.OnMouseMove(e);
        }
        protected override void OnMouseLeave(EventArgs e) { survol = Zone.Aucune; ligneSurvol = -1; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            int l; var z = Toucher(e.Location, out l);
            if (z == Zone.Aucune && e.Button == MouseButtons.Left)
            {
                // glisser la tele par son bois
                ReleaseCapture(); SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
                return;
            }
            enfonce = z;
            if (EstVolume(z) && e.Button == MouseButtons.Left) { tourne = z; pDepart = e.Location; angleAvant = Angle(z, e.Location); volFlottant = Volume(z); aTourne = false; Capture = true; }
            Invalidate();
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            int l; var z = Toucher(e.Location, out l);
            var avant = enfonce; enfonce = Zone.Aucune;
            if (tourne != Zone.Aucune)
            {
                var t = tourne; tourne = Zone.Aucune; Capture = false;
                if (!aTourne) { var c = Px(Zones[t]); Volume(t, (int)Math.Round(Volume(t) / 10.0) * 10 + (e.X < c.X + c.Width / 2 ? -10 : 10)); }
                Enregistrer(); Invalidate();
                base.OnMouseUp(e); return;
            }
            if (z == avant && z != Zone.Aucune) Agir(z, l, e.Button == MouseButtons.Right ? -1 : 1, e.Location);
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            int l; var z = Toucher(e.Location, out l);
            int s = e.Delta > 0 ? 1 : -1;
            if (z == Zone.Ligne || z == Zone.Voix || z == Zone.General || z == Zone.Effets || z == Zone.Musique || z == Zone.Page) Agir(z, l, s, e.Location);
            base.OnMouseWheel(e);
        }

        void Agir(Zone z, int ligne, int s, Point p)
        {
            switch (z)
            {
                case Zone.Voix: Volume(z, Volume(z) + 5 * s); break;
                case Zone.General: Volume(z, Volume(z) + 5 * s); break;
                case Zone.Effets: Volume(z, Volume(z) + 5 * s); break;
                case Zone.Musique: Volume(z, Volume(z) + 5 * s); break;
                case Zone.H: case Zone.M: case Zone.L: Prereglage(z); break;
                case Zone.Ecran: r.PleinEcran = !r.PleinEcran; Dire(r.PleinEcran ? "Fullscreen." : "Borderless window."); break;
                case Zone.Haut: selection = (selection - 1 + pages[page].Count) % pages[page].Count; break;
                case Zone.Bas: selection = (selection + 1) % pages[page].Count; break;
                case Zone.Page:
                    if (!Px(Zones[Zone.Page]).Contains(p)) page = Math.Max(0, Math.Min(titres.Count - 1, (int)((p.X - TexteX * S) / (TexteL * S / titres.Count))));
                    else page = (page + s + pages.Count) % pages.Count;
                    selection = 0; break;
                case Zone.Ligne: selection = ligne; pages[page][ligne].Changer(s); break;
                case Zone.Lancer: Jouer(); return;
                case Zone.Quitter: Close(); return;
            }
            Enregistrer();
            Invalidate();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            var opts = pages[page];
            switch (e.KeyCode)
            {
                case Keys.Up: selection = (selection - 1 + opts.Count) % opts.Count; break;
                case Keys.Down: selection = (selection + 1) % opts.Count; break;
                case Keys.Left: opts[selection].Changer(-1); Enregistrer(); break;
                case Keys.Right: opts[selection].Changer(1); Enregistrer(); break;
                case Keys.Tab: page = (page + (e.Shift ? -1 : 1) + pages.Count) % pages.Count; selection = 0; break;
                case Keys.Enter: Jouer(); break;
                case Keys.Escape: Close(); break;
                default: base.OnKeyDown(e); return;
            }
            e.Handled = true; Invalidate();
        }

        protected override bool IsInputKey(Keys k) { return k == Keys.Up || k == Keys.Down || k == Keys.Left || k == Keys.Right || k == Keys.Tab || base.IsInputKey(k); }

        void Dire(string t) { message = t; messageJusqua = DateTime.Now.AddSeconds(3); Invalidate(EcranPx()); }
        void Enregistrer() { try { r.Enregistrer(jeu.FichierReglages); } catch { } }

        // ------------------------------------------------------------------ actions

        void Jouer()
        {
            if (partie != null) { Dire("The game is already running."); return; }
            if (Process.GetProcessesByName("Devil").Length > 0)
            {
                MessageBox.Show(this, "Devil.exe is already running (maybe frozen and invisible). Close it first, with Task Manager if needed.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Enregistrer();
            Journal.Note("=== Lancement ===");
            Action<string> statut = s => BeginInvoke(new Action(() => Dire(s)));
            var reg = r;
            partie = Process.GetCurrentProcess(); // marqueur "lancement en cours"
            Invalidate();
            new Thread(() =>
            {
                try
                {
                    jeu.Preparer(reg, statut);
                    var p = jeu.Lancer();
                    BeginInvoke(new Action(() => { partie = p; Dire("The game is running."); WindowState = FormWindowState.Minimized; }));
                    p.WaitForExit();
                    Journal.Note("Devil.exe ferme (code {0})", p.ExitCode);
                    BeginInvoke(new Action(() => { partie = null; Dire("Game closed."); WindowState = FormWindowState.Normal; Activate(); Invalidate(); }));
                }
                catch (Exception ex)
                {
                    Journal.Note("ERREUR : {0}", ex.Message);
                    BeginInvoke(new Action(() =>
                    {
                        partie = null; Dire("Launch failed."); Invalidate();
                        MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }));
                }
            }) { IsBackground = true, Name = "lancement" }.Start();
        }

        void Restaurer()
        {
            if (partie != null) { Dire("Close the game first."); return; }
            if (MessageBox.Show(this, "Restore the original Devil.exe?\nEnabled patches are applied again on the next launch; turn them off to play without them.",
                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try { jeu.RestaurerExe(); Dire("Original Devil.exe restored."); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        protected override void Dispose(bool disposing)
        {
            if (surface != null) { surface.Dispose(); DeleteDC(dcSurface); DeleteObject(dibSurface); surface = null; }
            if (socle != null) { socle.Dispose(); socle = null; }
            if (disposing) { horloge.Dispose(); if (fond != null) fond.Dispose(); if (couche != null) couche.Dispose(); if (bande != null) bande.Dispose(); if (art != null) art.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
