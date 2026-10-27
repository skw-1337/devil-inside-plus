// Devil Inside Plus - preparation du dossier du jeu : wrapper dgVoodoo, Config.ini, dgVoodoo.conf,
// correctifs de Devil.exe (vérifiés octet par octet, réversibles) et lancement.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace DevilInsidePlus
{
    class Morceau
    {
        public int Offset; public byte[] Avant, Apres;
        public Morceau(int offset, byte[] avant, byte[] apres) { Offset = offset; Avant = avant; Apres = apres; }
    }

    class Correctif
    {
        public string Id, Description; public Morceau[] Morceaux;
        public Correctif(string id, string desc, params Morceau[] m) { Id = id; Description = desc; Morceaux = m; }
    }

    class Jeu
    {
        public readonly string Dossier, Reserve, Originaux;
        public string Exe { get { return Path.Combine(Dossier, "Devil.exe"); } }
        public string FichierReglages { get { return Path.Combine(Reserve, "reglages.ini"); } }

        // Offsets fichier de Devil.exe (version francaise ABWFR, 3 215 360 octets).
        // VA du code -> offset fichier : .text commence a VA 0x401000 / fichier 0x1000.
        const int SiteFov = 0x1B83D9, CaveFov = 0x84F0, ValeurFov = 0x8500;

        public static readonly Correctif[] Correctifs =
        {
            // L'horloge du moteur (0x5BB290) prend la frequence du TSC = MHz * 1 000 000 en entier 32 bits :
            // au-dela de 4294 MHz le calcul deborde et le jeu tourne plusieurs fois trop vite.
            // On saute le TSC : le moteur retombe sur QueryPerformanceCounter, exact sur tout PC.
            new Correctif("horloge", "Engine clock uses QueryPerformanceCounter (correct speed on CPUs above 4.3 GHz).",
                new Morceau(0x1BB2A5, new byte[] { 0x74, 0x68 }, new byte[] { 0xEB, 0x68 })),
            // Boucle "Inserez le CD" (0x4A2DB9) : on la saute, les donnees sont toutes sur le disque.
            new Correctif("cd", "No CD required.",
                new Morceau(0xA2DB9, new byte[] { 0x75, 0x36 }, new byte[] { 0xEB, 0x36 })),
            // Import WINMM.dll -> WinMX.dll (ogg-winmm) : la musique CD est lue depuis Music\TrackNN.ogg.
            new Correctif("musique", "CD music played from Music\\*.ogg (WinMX.dll).",
                new Morceau(0x2E5DEA, Encoding.ASCII.GetBytes("WINMM.dll"), Encoding.ASCII.GetBytes("WinMX.dll"))),
            // Projection camera (0x5B83B0) : focale = 640 / (2 tan(fov/2)) pour un ecran de reference 640x480,
            // puis l'image est mise a l'echelle par la largeur : en 16:9 le haut et le bas sont rognes (image "zoomee").
            // Le fild [ecx+0xC4] (largeur de reference) appelle une routine qui multiplie par K = (4/3) / (L/H) :
            // champ vertical identique au 4:3, champ horizontal elargi (Hor+). K est ecrit a 0x408500 au lancement.
            new Correctif("fov", "Hor+ field of view for widescreen (16:9, 21:9).",
                new Morceau(SiteFov, new byte[] { 0xDB, 0x81, 0xC4, 0x00, 0x00, 0x00 }, AppelRel(0x5B83D9, 0x4084F0)),
                new Morceau(CaveFov, Remplissage(13), new byte[] {
                    0xDB, 0x81, 0xC4, 0x00, 0x00, 0x00,     // fild dword [ecx+0xC4]
                    0xD8, 0x0D, 0x00, 0x85, 0x40, 0x00,     // fmul dword [0x408500]
                    0xC3 })),                               // ret
            // Lock d'une surface (0x59186C) : apres un Alt+Tab, DirectDraw renvoie DDERR_SURFACELOST et le moteur
            // s'arrete sur "CDirectDrawSurface::Lock". Le Flip (0x59503A) gere deja ce cas avec RestoreAllSurfaces
            // (0x595130) : on fait pareil pour Lock (restaure, attend 50 ms, reessaie, 20 fois au plus).
            new Correctif("surfaces", "Alt+Tab back without DirectDraw error (surfaces restored).",
                new Morceau(0x19186C, new byte[] { 0xFF, 0x50, 0x64, 0x85, 0xC0 }, new byte[] { 0xE8, 0x9F, 0x6C, 0xE7, 0xFF }),
                new Morceau(0x8510, Remplissage(65), new byte[] {
                    0x53, 0xBB, 0x14, 0x00, 0x00, 0x00,                         // push ebx ; mov ebx,20
                    0xFF, 0x74, 0x24, 0x18, 0xFF, 0x74, 0x24, 0x18, 0xFF, 0x74, 0x24, 0x18,
                    0xFF, 0x74, 0x24, 0x18, 0xFF, 0x74, 0x24, 0x18,             // re-empile les 5 arguments de Lock
                    0x8B, 0x04, 0x24, 0x8B, 0x00, 0xFF, 0x50, 0x64,             // call IDirectDrawSurface::Lock
                    0x3D, 0xC2, 0x01, 0x76, 0x88, 0x75, 0x12,                   // cmp eax,DDERR_SURFACELOST ; jne fin
                    0x4B, 0x74, 0x0F,                                           // dec ebx ; jz fin
                    0xE8, 0xEF, 0xCB, 0x18, 0x00,                               // call 0x595130 (RestoreAllSurfaces)
                    0x6A, 0x32, 0xFF, 0x15, 0x34, 0xB1, 0xBC, 0x00,             // Sleep(50)
                    0xEB, 0xCB,                                                 // jmp reessai
                    0x5B, 0x85, 0xC0, 0xC2, 0x14, 0x00 })),                     // fin : pop ebx ; test eax,eax ; ret 0x14
            // DirectInput (DXDispatch) : sans le focus, Acquire du clavier/souris echoue (DIERR_OTHERAPPHASPRIO) et le
            // moteur s'arrete sur "cannot acquire keyboard/mouse". Lectures par image (0x593C40 clavier, 0x593D34 souris) :
            // on sort de la boucle avec l'erreur, deja geree (aucune entree cette image). Les deux autres lectures
            // (0x511CD3, 0x57E881) ne testent pas le resultat : elles attendent 50 ms et reessaient au lieu de s'arreter.
            new Correctif("entrees", "Keyboard and mouse: no fatal error without focus (Alt+Tab).",
                new Morceau(0x111CD3, new byte[] { 0x50, 0x68, 0x24, 0xCB, 0x6C, 0x00, 0xE8, 0x12, 0x38, 0x08 },
                                      new byte[] { 0x6A, 0x32, 0xFF, 0x15, 0x34, 0xB1, 0xBC, 0x00, 0xEB, 0x04 }),
                new Morceau(0x17E881, new byte[] { 0x50, 0x68, 0x24, 0xCB, 0x6C, 0x00, 0xE8, 0x64, 0x6C, 0x01 },
                                      new byte[] { 0x6A, 0x32, 0xFF, 0x15, 0x34, 0xB1, 0xBC, 0x00, 0xEB, 0x04 }),
                new Morceau(0x193C40, new byte[] { 0x50, 0x68 }, new byte[] { 0xEB, 0xB1 }),
                new Morceau(0x193D34, new byte[] { 0x50, 0x68 }, new byte[] { 0xEB, 0xB8 })),
        };

        static byte[] Remplissage(int n) { return Enumerable.Repeat((byte)0xCC, n).ToArray(); }

        static byte[] AppelRel(int depuis, int vers)
        {
            var b = new byte[6]; b[0] = 0xE8; BitConverter.GetBytes(vers - (depuis + 5)).CopyTo(b, 1); b[5] = 0x90; return b;
        }

        /// <summary>Facteur applique a la focale : Hor+ (4/3 divise par le format de l'ecran), elargi du bonus demande.</summary>
        public static float FacteurFov(Reglages r)
        {
            int w, h; ResolutionEffective(r, out w, out h);
            return (float)((4.0 / 3.0) / ((double)w / h) * 100.0 / (100.0 + Math.Max(0, r.FovBonus)));
        }

        public Jeu(string dossier)
        {
            Dossier = dossier;
            Reserve = Path.Combine(dossier, "_DevilInsidePlus");
            Originaux = Path.Combine(Reserve, "originaux");
            Directory.CreateDirectory(Originaux);
        }

        public bool Actif(Reglages r, Correctif c)
        {
            return c.Id == "horloge" ? r.PatchHorloge : c.Id == "cd" ? r.PatchCD : c.Id == "fov" ? r.PatchFov : c.Id == "surfaces" ? r.PatchSurfaces : c.Id == "entrees" ? r.PatchEntrees : r.PatchMusique;
        }

        // ------------------------------------------------------------------ correctifs de l'exe

        static bool Egal(byte[] d, int o, byte[] m) { if (o + m.Length > d.Length) return false; for (int i = 0; i < m.Length; i++) if (d[o + i] != m[i]) return false; return true; }

        static bool Tous(byte[] d, Correctif c, bool apres) { return c.Morceaux.All(m => Egal(d, m.Offset, apres ? m.Apres : m.Avant)); }

        /// <summary>"applique", "absent" ou "inconnu" pour l'exe actuel.</summary>
        public string Etat(Correctif c)
        {
            var d = File.ReadAllBytes(Exe);
            return Tous(d, c, true) ? "applique" : Tous(d, c, false) ? "absent" : "inconnu";
        }

        public void AppliquerCorrectifs(Reglages r)
        {
            var d = File.ReadAllBytes(Exe);
            foreach (var c in Correctifs)
                foreach (var m in c.Morceaux)
                    if (!Egal(d, m.Offset, m.Avant) && !Egal(d, m.Offset, m.Apres))
                        throw new InvalidOperationException("Devil.exe is not the expected version (unexpected bytes for patch \"" + c.Id + "\"). Nothing was changed.");

            // copie de l'exe d'origine (tous correctifs retires), une seule fois
            string orig = Path.Combine(Originaux, "Devil.exe");
            if (!File.Exists(orig))
            {
                var o = (byte[])d.Clone();
                foreach (var c in Correctifs) foreach (var m in c.Morceaux) Array.Copy(m.Avant, 0, o, m.Offset, m.Avant.Length);
                Array.Copy(Remplissage(4), 0, o, ValeurFov, 4);
                File.WriteAllBytes(orig, o);
                Journal.Note("Devil.exe d'origine sauvegarde dans {0}", orig.Substring(Dossier.Length + 1));
            }

            bool winmx = File.Exists(Path.Combine(Dossier, "WinMX.dll"));
            var avant = (byte[])d.Clone();
            foreach (var c in Correctifs)
            {
                bool voulu = Actif(r, c) && (c.Id != "musique" || winmx);
                if (c.Id == "musique" && r.PatchMusique && !winmx) Journal.Note("WinMX.dll absent : musique CD desactivee");
                foreach (var m in c.Morceaux) { var v = voulu ? m.Apres : m.Avant; Array.Copy(v, 0, d, m.Offset, v.Length); }
                if (c.Id == "fov")
                {
                    var k = voulu ? BitConverter.GetBytes(FacteurFov(r)) : Remplissage(4);
                    Array.Copy(k, 0, d, ValeurFov, 4);
                    if (voulu) Journal.Note("  champ de vision : facteur focale {0:0.000} (bonus {1} %)", FacteurFov(r), r.FovBonus);
                }
                Journal.Note("  correctif {0} : {1}", c.Id, voulu ? "applique" : "retire");
            }
            if (!d.SequenceEqual(avant)) File.WriteAllBytes(Exe, d);
        }

        // ------------------------------------------------------------------ wrapper dgVoodoo

        const string UrlDgVoodoo = "https://github.com/dege-diosg/dgVoodoo2/releases/download/v2.87.5/dgVoodoo2_87_5.zip";
        const string ShaDgVoodoo = "5FFDE6927F7355CA3FDD5D785B581256A8E6539FA13E395A891ADE6BA1040850";

        static bool EstDgVoodoo(string f)
        {
            if (!File.Exists(f)) return false;
            var v = FileVersionInfo.GetVersionInfo(f);
            return (v.ProductName ?? "").IndexOf("dgVoodoo", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public void InstallerDgVoodoo(Action<string> statut)
        {
            string dd = Path.Combine(Dossier, "ddraw.dll"), imm = Path.Combine(Dossier, "D3DImm.dll");
            if (EstDgVoodoo(dd) && File.Exists(imm)) { Journal.Note("dgVoodoo {0} present", FileVersionInfo.GetVersionInfo(dd).ProductVersion); return; }
            // ancien wrapper (DDrawCompat 2016 : aucun mode 16 bits, faisait planter le setup) mis de cote
            if (File.Exists(dd))
            {
                string dest = Path.Combine(Originaux, "ddraw." + DateTime.Now.ToString("yyyyMMddHHmmss") + ".dll");
                File.Move(dd, dest);
                Journal.Note("Ancien ddraw.dll deplace vers {0}", dest.Substring(Dossier.Length + 1));
            }
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; // TLS 1.2
            string zip = Path.Combine(Reserve, "dgVoodoo2_87_5.zip");
            if (!File.Exists(zip) || Sha256(zip) != ShaDgVoodoo)
            {
                statut("Downloading dgVoodoo 2.87.5…");
                Journal.Note("Telechargement : {0}", UrlDgVoodoo);
                using (var wc = new WebClient()) { wc.Headers[HttpRequestHeader.UserAgent] = "DevilInsidePlus/" + Version.Texte; wc.DownloadFile(UrlDgVoodoo, zip + ".part"); }
                if (File.Exists(zip)) File.Delete(zip);
                File.Move(zip + ".part", zip);
            }
            if (Sha256(zip) != ShaDgVoodoo) throw new InvalidOperationException("The downloaded dgVoodoo does not match the expected SHA-256. Nothing was installed.");
            using (var z = ZipFile.OpenRead(zip))
            {
                Extraire(z, "MS/x86/DDraw.dll", dd);
                Extraire(z, "MS/x86/D3DImm.dll", imm);
                if (!File.Exists(Path.Combine(Dossier, "dgVoodoo.conf"))) Extraire(z, "dgVoodoo.conf", Path.Combine(Dossier, "dgVoodoo.conf"));
            }
            Journal.Note("dgVoodoo 2.87.5 (Dege, http://dege.fw.hu) installe depuis la release officielle");
        }

        static string Sha256(string f)
        {
            using (var s = SHA256.Create()) using (var fs = File.OpenRead(f))
                return BitConverter.ToString(s.ComputeHash(fs)).Replace("-", "");
        }

        static void Extraire(ZipArchive z, string entree, string cible)
        {
            var e = z.Entries.FirstOrDefault(x => string.Equals(x.FullName.Replace('\\', '/'), entree, StringComparison.OrdinalIgnoreCase));
            if (e == null) throw new InvalidOperationException("Incomplete dgVoodoo archive: " + entree);
            e.ExtractToFile(cible, true);
        }

        // ------------------------------------------------------------------ fichiers de configuration

        public static void ResolutionEffective(Reglages r, out int w, out int h)
        {
            if (!Reglages.LireRes(r.Resolution, out w, out h))
            {
                var b = Screen.PrimaryScreen.Bounds; w = b.Width; h = b.Height;
            }
        }

        static readonly Encoding Latin1 = Encoding.GetEncoding(1252);

        /// <summary>Remplace "cle = valeur" dans la section donnee (ajoute la cle si absente).</summary>
        static void Poser(List<string> lignes, string section, string cle, string valeur, bool aligne)
        {
            int debut = lignes.FindIndex(l => l.Trim().Equals("[" + section + "]", StringComparison.OrdinalIgnoreCase));
            if (debut < 0) { lignes.Add(""); lignes.Add("[" + section + "]"); debut = lignes.Count - 1; }
            int fin = lignes.FindIndex(debut + 1, l => l.TrimStart().StartsWith("["));
            if (fin < 0) fin = lignes.Count;
            var re = new Regex(@"^\s*" + Regex.Escape(cle) + @"\s*=", RegexOptions.IgnoreCase);
            string texte = aligne ? cle.PadRight(36) + "= " + valeur : cle + " = " + valeur;
            for (int i = debut + 1; i < fin; i++)
                if (re.IsMatch(lignes[i]))
                {
                    if (aligne) { int eg = lignes[i].IndexOf('='); texte = lignes[i].Substring(0, eg + 1) + " " + valeur; }
                    lignes[i] = texte; return;
                }
            // insere apres la derniere ligne non vide de la section
            int pos = fin; while (pos > debut + 1 && lignes[pos - 1].Trim() == "") pos--;
            lignes.Insert(pos, texte);
        }

        public void EcrireConfigIni(Reglages r)
        {
            string f = Path.Combine(Dossier, "Config.ini");
            var l = File.Exists(f) ? File.ReadAllLines(f, Latin1).ToList() : new List<string>();
            if (l.Count == 0) Journal.Note("Config.ini absent : cree");
            int w, h; ResolutionEffective(r, out w, out h);
            Poser(l, "VIDEO", "Resolution", w + "x" + h, false);
            Poser(l, "VIDEO", "Device", "display", false);
            Poser(l, "DETAILS", "Lighting", r.Eclairage, false);
            Poser(l, "DETAILS", "ShadowMap", r.Ombres, false);
            Poser(l, "DETAILS", "TextureDetails", r.Textures, false);
            Poser(l, "GAME", "SlowMotion", r.Ralenti ? "TRUE" : "FALSE", false);
            Poser(l, "GAME", "Mirrors", r.Miroirs ? "TRUE" : "FALSE", false);
            Poser(l, "GAME", "FloatingCameras", r.CamerasFlottantes ? "TRUE" : "FALSE", false);
            Poser(l, "GAME", "GameTextes", r.Soustitres ? "TRUE" : "FALSE", false);
            Poser(l, "SOUND", "GeneralVolume", r.VolumeGeneral.ToString(), false);
            Poser(l, "SOUND", "SFXVolume", r.VolumeEffets.ToString(), false);
            Poser(l, "SOUND", "CDVolume", r.VolumeMusique.ToString(), false);
            Poser(l, "SOUND", "VoiceVolume", r.VolumeVoix.ToString(), false);
            Poser(l, "SOUND", "EAX", r.Eax ? "TRUE" : "FALSE", false);
            Poser(l, "LANGUAGE", "Language", "Fr", false);
            File.WriteAllLines(f, l, Latin1);
            Journal.Note("Config.ini : {0}x{1}, eclairage {2}, ombres {3}, textures {4}", w, h, r.Eclairage, r.Ombres, r.Textures);
        }

        public void EcrireDgVoodooConf(Reglages r)
        {
            string f = Path.Combine(Dossier, "dgVoodoo.conf");
            var l = File.ReadAllLines(f).ToList();
            int w, h; ResolutionEffective(r, out w, out h);
            Poser(l, "General", "OutputAPI", "d3d11_fl11_0", true);
            Poser(l, "General", "FullScreenMode", r.PleinEcran ? "true" : "false", true);
            Poser(l, "General", "ScalingMode", "stretched_ar", true);
            Poser(l, "General", "KeepWindowAspectRatio", "true", true);
            Poser(l, "GeneralExt", "WindowedAttributes", "borderless, fullscreensize", true);
            Poser(l, "GeneralExt", "FPSLimit", r.FpsMax.ToString(), true);
            Poser(l, "GeneralExt", "WatermarkDisplayDuration", "0", true);
            Poser(l, "DirectX", "VideoCard", "internal3D", true);
            Poser(l, "DirectX", "VRAM", "1024", true);
            Poser(l, "DirectX", "Resolution", r.Surechantillonnage > 1 ? r.Surechantillonnage + "x" : "unforced", true);
            Poser(l, "DirectX", "Antialiasing", r.Antialiasing, true);
            Poser(l, "DirectX", "Filtering", r.Anisotrope > 1 ? r.Anisotrope.ToString() : "appdriven", true);
            Poser(l, "DirectX", "Mipmapping", r.MipmapsAuto ? "autogen_bilinear" : "appdriven", true);
            Poser(l, "DirectX", "ForceVerticalSync", r.VSync ? "true" : "false", true);
            Poser(l, "DirectX", "dgVoodooWatermark", "false", true);
            Poser(l, "DirectX", "AppControlledScreenMode", "false", true);
            Poser(l, "DirectX", "DisableAltEnterToToggleScreenMode", "true", true);
            Poser(l, "DirectX", "Bilinear2DOperations", "true", true);
            // le moteur rend nativement a n'importe quelle resolution : on s'assure qu'elle est proposee
            Poser(l, "DirectXExt", "ExtraEnumeratedResolutions", w + "x" + h, true);
            Poser(l, "DirectXExt", "EnumeratedResolutionBitdepths", "all", true);
            File.WriteAllLines(f, l);
            Journal.Note("dgVoodoo.conf : {0}, AA {1}, AF {2}, {3} FPS, rendu {4}", r.PleinEcran ? "plein écran" : "fenêtre sans bordure",
                r.Antialiasing, r.Anisotrope, r.FpsMax, r.Surechantillonnage > 1 ? r.Surechantillonnage + "x" : "natif");
        }

        // ------------------------------------------------------------------ lancement

        public void Preparer(Reglages r, Action<string> statut)
        {
            statut("Checking dgVoodoo…");
            InstallerDgVoodoo(statut);
            statut("Writing configuration…");
            EcrireConfigIni(r);
            EcrireDgVoodooConf(r);
            statut("Patching the engine…");
            AppliquerCorrectifs(r);
        }

        public Process Lancer()
        {
            var psi = new ProcessStartInfo(Exe) { WorkingDirectory = Dossier, UseShellExecute = false };
            var p = Process.Start(psi);
            Journal.Note("Devil.exe lance (pid {0})", p.Id);
            return p;
        }

        /// <summary>Remet l'exe d'origine (les correctifs se reappliquent au prochain lancement selon les cases).</summary>
        public void RestaurerExe()
        {
            string orig = Path.Combine(Originaux, "Devil.exe");
            if (!File.Exists(orig)) throw new InvalidOperationException("No original copy of Devil.exe.");
            File.Copy(orig, Exe, true);
            Journal.Note("Devil.exe d'origine restaure");
        }
    }
}
