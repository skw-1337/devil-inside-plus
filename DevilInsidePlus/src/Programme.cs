// Devil Inside Plus - point d'entree et journal.
using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace DevilInsidePlus
{
    static class Programme
    {
        public const string DossierParDefaut = @"C:\Jeux\ABWFR\The Devil Inside";

        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string jeu = TrouverJeu();
            if (jeu == null)
            {
                MessageBox.Show("Devil.exe not found.\n\nPut DevilInsidePlus.exe in the game folder (next to Devil.exe).",
                    "Devil Inside Plus", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            var j = new Jeu(jeu);
            Journal.Ouvrir(Path.Combine(j.Reserve, "journal.txt"));
            Journal.Note("Devil Inside Plus v{0} - jeu : {1}", Version.Texte, jeu);
            bool direct = args.Any(a => a.Equals("/jouer", StringComparison.OrdinalIgnoreCase));
            // /capture fichier.png [page] : enregistre le rendu du lanceur (fond transparent) puis quitte
            int ic = Array.FindIndex(args, a => a.Equals("/capture", StringComparison.OrdinalIgnoreCase));
            var f = new Fenetre(j, direct);
            if (ic >= 0 && ic + 1 < args.Length)
            {
                int page = 0; if (ic + 2 < args.Length) int.TryParse(args[ic + 2], out page);
                f.Capturer(args[ic + 1], page);
                return;
            }
            Application.Run(f);
        }

        static string TrouverJeu()
        {
            foreach (var d in new[] { AppDomain.CurrentDomain.BaseDirectory, Environment.CurrentDirectory, DossierParDefaut })
                if (File.Exists(Path.Combine(d, "Devil.exe"))) return Path.GetFullPath(d).TrimEnd('\\');
            return null;
        }
    }

    /// <summary>Journal texte (fichier + abonnes de la fenetre).</summary>
    static class Journal
    {
        public static string Fichier;
        public static event Action<string> Ligne;
        static readonly object verrou = new object();

        public static void Ouvrir(string f)
        {
            Fichier = f;
            try { if (File.Exists(f) && new FileInfo(f).Length > 512 * 1024) File.Delete(f); } catch { }
        }

        public static void Note(string format, params object[] a)
        {
            string l = DateTime.Now.ToString("HH:mm:ss ") + (a.Length > 0 ? string.Format(format, a) : format);
            lock (verrou) { try { if (Fichier != null) File.AppendAllText(Fichier, l + "\r\n"); } catch { } }
            var h = Ligne; if (h != null) h(l);
        }
    }
}
