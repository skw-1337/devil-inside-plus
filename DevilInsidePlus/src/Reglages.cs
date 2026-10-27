// Devil Inside Plus - reglages du lanceur (fichier cle = valeur dans la reserve du jeu).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DevilInsidePlus
{
    class Reglages
    {
        // Affichage
        public string Resolution = "auto";          // "auto" = resolution native de l'ecran principal, sinon "LxH"
        public int Surechantillonnage = 1;          // 1 = natif, 2/3 = rendu 2x/3x puis reduit (dgVoodoo)
        public bool PleinEcran = false;             // faux = fenetre sans bordure plein ecran
        public int FpsMax = 60;
        public bool VSync = true;
        // Rendu dgVoodoo
        public string Antialiasing = "8x";
        public int Anisotrope = 16;
        public bool MipmapsAuto = true;
        // Details du moteur (Config.ini)
        public string Eclairage = "HIGH", Ombres = "HIGH", Textures = "HIGH";
        public bool Miroirs = true, CamerasFlottantes = true, Ralenti = true, Soustitres = true;
        // Son (Config.ini)
        public int VolumeGeneral = 80, VolumeEffets = 70, VolumeMusique = 70, VolumeVoix = 70;
        public bool Eax = true;
        // Correctifs de l'exe
        public bool PatchHorloge = true, PatchCD = true, PatchMusique = true, PatchFov = true, PatchSurfaces = true, PatchEntrees = true;
        public int FovBonus = 0;                    // % de champ horizontal en plus, au-dela du Hor+

        static IEnumerable<FieldInfo> Champs() { return typeof(Reglages).GetFields(BindingFlags.Public | BindingFlags.Instance); }

        public static Reglages Charger(string f)
        {
            var r = new Reglages();
            if (!File.Exists(f)) return r;
            foreach (var l in File.ReadAllLines(f))
            {
                int i = l.IndexOf('='); if (i <= 0) continue;
                var c = typeof(Reglages).GetField(l.Substring(0, i).Trim());
                if (c == null) continue;
                string v = l.Substring(i + 1).Trim();
                try
                {
                    if (c.FieldType == typeof(int)) c.SetValue(r, int.Parse(v, CultureInfo.InvariantCulture));
                    else if (c.FieldType == typeof(bool)) c.SetValue(r, v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase));
                    else c.SetValue(r, v);
                }
                catch { }
            }
            return r;
        }

        public void Enregistrer(string f)
        {
            File.WriteAllLines(f, EnPaires().Select(kv => kv.Key + " = " + kv.Value).ToArray());
        }

        public IEnumerable<KeyValuePair<string, string>> EnPaires()
        {
            foreach (var c in Champs())
            {
                object v = c.GetValue(this);
                yield return new KeyValuePair<string, string>(c.Name, v is bool ? ((bool)v ? "1" : "0") : Convert.ToString(v, CultureInfo.InvariantCulture));
            }
        }

        public static bool LireRes(string s, out int w, out int h)
        {
            w = h = 0;
            var p = (s ?? "").ToLowerInvariant().Split('x');
            return p.Length == 2 && int.TryParse(p[0].Trim(), out w) && int.TryParse(p[1].Trim(), out h) && w > 0 && h > 0;
        }
    }
}
