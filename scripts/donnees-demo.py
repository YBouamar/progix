#!/usr/bin/env python3
"""Remplit PROGIX de données fictives en passant par l'API, pour que toutes les règles métier s'appliquent.

Usage : docker compose --profile demo run --rm demo
   ou : python3 scripts/donnees-demo.py [http://localhost:8080]
Toutes les sociétés, personnes et montants sont inventés.
"""
import json, os, struct, sys, zlib, urllib.request, urllib.error, http.cookiejar
from datetime import date, timedelta

API = (sys.argv[1] if len(sys.argv) > 1 else os.environ.get("API_URL", "http://localhost:8080")).rstrip("/")
MDP = os.environ.get("DEMO_PASSWORD", "Progix#2026")
AUJ = date.today()
def jour(n): return (AUJ - timedelta(days=n)).isoformat()


class Erreur(Exception):
    pass


class Session:
    """Une session par compte : le cookie de connexion est gardé."""
    def __init__(self, login):
        self.login = login
        self.o = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
        self.post("/auth/login", {"login": login, "password": MDP})

    def appel(self, methode, chemin, corps=None, brut=None, type_contenu="application/json"):
        data = brut if brut is not None else (json.dumps(corps).encode() if corps is not None else None)
        r = urllib.request.Request(API + "/api" + chemin, data=data, method=methode)
        if data is not None: r.add_header("Content-Type", type_contenu)
        try:
            with self.o.open(r, timeout=120) as rep:
                t = rep.read()
                return json.loads(t) if t and "json" in (rep.headers.get("Content-Type") or "") else None
        except urllib.error.HTTPError as e:
            t = e.read().decode("utf-8", "replace")
            try:
                j = json.loads(t); t = j.get("message") or json.dumps(j.get("errors") or j, ensure_ascii=False)
            except Exception: pass
            raise Erreur(f"{self.login} {methode} {chemin} -> {e.code} {t[:400]}") from None

    def get(self, c): return self.appel("GET", c)
    def post(self, c, corps=None): return self.appel("POST", c, corps if corps is not None else {})
    def put(self, c, corps): return self.appel("PUT", c, corps)

    def fichier(self, contexte, cible, nom, contenu, type_contenu, categorie=None):
        f = self.post("/fichiers/envois", {"contexte": contexte, "cibleId": cible, "nom": nom, "taille": len(contenu), "contentType": type_contenu, "categorie": categorie})
        m = self.appel("PUT", f"/fichiers/envois/{f['id']}/morceaux/1", brut=contenu, type_contenu="application/octet-stream")
        self.post(f"/fichiers/envois/{f['id']}/terminer", {"morceaux": [{"numero": 1, "eTag": m["eTag"]}]})


def png(r, v, b, cote=240):
    """Petite image unie, tenant lieu de photo de chantier."""
    def bloc(t, d): return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xFFFFFFFF)
    ligne = b"\x00" + bytes([r, v, b]) * cote
    return b"\x89PNG\r\n\x1a\n" + bloc(b"IHDR", struct.pack(">IIBBBBB", cote, cote, 8, 2, 0, 0, 0)) + bloc(b"IDAT", zlib.compress(ligne * cote)) + bloc(b"IEND", b"")


def pdf(titre):
    """PDF d'une page portant un titre, tenant lieu de plan ou de rapport."""
    flux = f"BT /F1 16 Tf 60 780 Td ({titre}) Tj 0 -24 Td /F1 10 Tf (Document fictif de demonstration PROGIX) Tj ET".encode("latin-1", "replace")
    objets = [b"<< /Type /Catalog /Pages 2 0 R >>", b"<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
              b"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
              b"<< /Length %d >>\nstream\n" % len(flux) + flux + b"\nendstream", b"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"]
    s, pos = b"%PDF-1.4\n", []
    for i, o in enumerate(objets):
        pos.append(len(s)); s += b"%d 0 obj\n" % (i + 1) + o + b"\nendobj\n"
    x = len(s)
    s += b"xref\n0 %d\n0000000000 65535 f \n" % (len(objets) + 1) + b"".join(b"%010d 00000 n \n" % p for p in pos)
    return s + b"trailer\n<< /Size %d /Root 1 0 R >>\nstartxref\n%d\n%%%%EOF\n" % (len(objets) + 1, x)


def dire(*a): print(*a, flush=True)
def dh(v): return f"{v:,.0f}".replace(",", " ") + " DH"


# --------------------------------------------------------------------------- données

CLIENTS = [
    dict(nature="morale", raisonSociale="Atlas Promotion Immobilière SARL", ice="002145698000047", rc="412587", identifiantFiscal="40125874", patente="35214789",
         adresse="27, boulevard Zerktouni, 3e étage", ville="Casablanca", telephone="0522 48 15 60", email="contact@atlas-promotion.example",
         contacts=[dict(nom="Karim Bennani", fonction="Gérant", telephone="0661 20 45 78", email="k.bennani@atlas-promotion.example")]),
    dict(nature="morale", raisonSociale="Marina Développement SA", ice="001874523000061", rc="98452", identifiantFiscal="20547812", patente="50214536",
         adresse="Avenue Mohammed VI, immeuble Détroit", ville="Tanger", telephone="0539 32 10 44", email="projets@marina-dev.example",
         contacts=[dict(nom="Salma El Fassi", fonction="Directrice de projets", telephone="0662 71 03 19", email="s.elfassi@marina-dev.example"),
                   dict(nom="Hicham Tazi", fonction="Responsable achats", telephone="0663 55 80 12", email="h.tazi@marina-dev.example")]),
    dict(nature="morale", raisonSociale="Fondation Atlas Santé", ice="003025417000015", rc="74125", identifiantFiscal="60214785", patente="36152347",
         adresse="Route de l'Ourika, km 4", ville="Marrakech", telephone="0524 30 77 20", email="direction@atlas-sante.example", exonereTva=True,
         attestationNumero="EXO-2026-01847", attestationExpiration=(AUJ + timedelta(days=200)).isoformat(),
         contacts=[dict(nom="Dr Nadia Chraibi", fonction="Directrice générale", telephone="0661 90 14 36", email="n.chraibi@atlas-sante.example")]),
    dict(nature="morale", raisonSociale="Logis Trans Maroc SARL", ice="002698741000083", rc="365214", identifiantFiscal="45871236", patente="36304694",
         adresse="Zone industrielle, lot 114", ville="Tit Mellil", telephone="0522 33 62 90", email="travaux@logistrans.example",
         contacts=[dict(nom="Omar Lahlou", fonction="Directeur technique", telephone="0664 12 78 40", email="o.lahlou@logistrans.example")]),
    dict(nature="morale", raisonSociale="Agdal Hôtellerie SA", ice="001532648000029", rc="125478", identifiantFiscal="33012547", patente="36457041",
         adresse="12, avenue de France, Agdal", ville="Rabat", telephone="0537 68 20 15", email="siege@agdal-hotellerie.example",
         contacts=[dict(nom="Leila Amrani", fonction="Maître d'ouvrage délégué", telephone="0661 47 22 05", email="l.amrani@agdal-hotellerie.example")]),
    dict(nature="morale", raisonSociale="Souss Habitat SARL", ice="002987456000052", rc="45217", identifiantFiscal="70125489", patente="36609388",
         adresse="Boulevard Hassan II, résidence Tifaouine", ville="Agadir", telephone="0528 84 03 31", email="contact@souss-habitat.example",
         contacts=[dict(nom="Brahim Aït Lahcen", fonction="Gérant", telephone="0662 30 91 57", email="b.aitlahcen@souss-habitat.example")]),
    dict(nature="morale", raisonSociale="Riad Salam Immobilier SARL", ice="003154782000038", rc="62014", identifiantFiscal="15478963", patente="36761735",
         adresse="45, avenue Hassan II", ville="Fès", telephone="0535 62 41 08", email="contact@riadsalam-immo.example",
         contacts=[dict(nom="Youssef Kettani", fonction="Gérant", telephone="0663 08 64 21", email="y.kettani@riadsalam-immo.example")]),
    dict(nature="physique", civilite="M.", nomFamille="Berrada", prenom="Mehdi", cin="BK514782", nationalite="Marocaine",
         adresse="Lot 38, Golf City", ville="Bouskoura", telephone="0661 55 02 73", email="mehdi.berrada@courriel.example", contacts=[]),
    dict(nature="physique", civilite="Mme", nomFamille="Idrissi", prenom="Ghita", nationalite="Marocaine",
         adresse="Lotissement Les Dunes, villa 12", ville="Dar Bouazza", telephone="0662 14 88 30", email="ghita.idrissi@courriel.example", contacts=[]),
    dict(nature="morale", raisonSociale="Oasis Retail Invest SA", ice="001245789000074", rc="287451", identifiantFiscal="25634178", patente="36914082",
         adresse="Route d'El Jadida, km 7", ville="Casablanca", telephone="0522 90 17 42", email="developpement@oasis-retail.example",
         contacts=[dict(nom="Rachid Mansouri", fonction="Directeur du développement", telephone="0661 73 40 96", email="r.mansouri@oasis-retail.example")]),
]

PARTENAIRES = [
    ("bet", "BET Structures Maghreb", "Casablanca", "Amine Ouazzani", "Ingénieur structure"),
    ("bet", "Ingénierie Rif Études", "Tanger", "Fatima Zahra Rahmouni", "Chef de projet"),
    ("bct", "Contrôle Technique Atlas", "Casablanca", "Mustapha Sebti", "Contrôleur technique"),
    ("bct", "Véritech Contrôle", "Rabat", "Imane Belkadi", "Ingénieure contrôle"),
    ("architecte", "Cabinet Alami Architectes", "Casablanca", "Driss Alami", "Architecte"),
    ("architecte", "Atelier Médina Architecture", "Marrakech", "Sofia Bennis", "Architecte"),
    ("laboratoire", "Laboratoire Essais et Matériaux du Centre", "Casablanca", "Khalid Zniber", "Responsable essais béton"),
    ("laboratoire", "Labo Nord Géotechnique", "Tanger", "Samira Haddou", "Responsable laboratoire"),
    ("entreprise_construction", "Grands Travaux du Sahel", "Casablanca", "Abdelilah Mouline", "Conducteur de travaux"),
    ("entreprise_construction", "Bâtisseurs de l'Atlas", "Marrakech", "Said Ouhmani", "Chef de chantier gros œuvre"),
]

# cibles : nombre d'étapes faites par dalle ; "4v" = commande usine saisie, en attente de ses validations.
PROJETS = [
    dict(cle="anfa", libelle="Résidence Les Jardins d'Anfa", entite="BBR", client=0, type="immeuble", ville="Casablanca", adresse="Angle boulevard d'Anfa et rue Ibn Sina", age=150,
         bet=0, bct=0, archi=0, labo=0, description="Immeuble R+5 sur deux sous-sols, planchers en post-tension.",
         dalles=[("A", "1", 0, 1240.0), ("A", "1", 1, 1180.5), ("A", "1", 2, 1180.5), ("A", "1", 3, 1180.5), ("A", "1", 4, 1096.0)], cibles=[11, 11, 11, 11, 11]),
    dict(cle="golf", libelle="Villa Bouskoura Golf", entite="APT", client=7, type="villa", ville="Bouskoura", adresse="Lot 38, Golf City", age=120,
         bet=0, bct=0, archi=0, labo=0, description="Villa R+1 avec porte-à-faux sur la terrasse.",
         dalles=[(None, None, 0, 412.0), (None, None, 1, 356.5)], cibles=[11, 11]),
    dict(cle="marina", libelle="Tour Marina Business Center", entite="APT", client=1, type="immeuble", ville="Tanger", adresse="Marina Bay, lot T4", age=100,
         bet=1, bct=1, archi=0, labo=1, description="Tour de bureaux R+12, plateaux libres de 16 m de portée.",
         dalles=[("T", "1", 0, 1560.0), ("T", "1", 1, 1420.0), ("T", "1", 2, 1420.0), ("T", "1", 3, 1420.0), ("T", "1", 4, 1420.0), ("T", "1", 5, 1420.0)], cibles=[11, 11, 9, 7, 4, 2]),
    dict(cle="clinique", libelle="Clinique Atlas Santé", entite="BBR", client=2, type="immeuble", ville="Marrakech", adresse="Route de l'Ourika, km 4", age=80,
         bet=0, bct=1, archi=1, labo=0, description="Clinique R+3, blocs opératoires au 1er étage.",
         dalles=[("B", "1", 0, 980.0), ("B", "1", 1, 940.0), ("B", "1", 2, 940.0), ("B", "1", 3, 880.0)], cibles=[10, 8, 5, 3]),
    dict(cle="logis", libelle="Plateforme logistique Tit Mellil", entite="APT", client=3, type="dallage", ville="Tit Mellil", adresse="Zone industrielle, lot 114", age=60,
         bet=0, bct=0, archi=None, labo=0, description="Dallage industriel sans joints, trois zones de stockage.",
         dalles=[("Z1", None, 0, 4200.0), ("Z2", None, 0, 3850.0), ("Z3", None, 0, 3600.0)], cibles=[6, "4v", 1]),
    dict(cle="agdal", libelle="Radier Hôtel Agdal", entite="BBR", client=4, type="radier", ville="Rabat", adresse="12, avenue de France, Agdal", age=40,
         bet=0, bct=1, archi=0, labo=0, description="Radier général précontraint sous hôtel R+7.",
         dalles=[("R", None, -2, 2150.0), ("R", None, -1, 1980.0)], cibles=[3, 0]),
    dict(cle="massira", libelle="Résidence Al Massira", entite="APT", client=5, type="immeuble", ville="Agadir", adresse="Boulevard Hassan II, lot 22", age=25,
         bet=0, bct=0, archi=1, labo=None, description="Résidence R+4, 32 logements.",
         dalles=[("A", "1", 0, 760.0), ("A", "1", 1, 720.0), ("A", "1", 2, 720.0), ("A", "1", 3, 720.0)], cibles=[1, 0, 0, 0]),
    dict(cle="riad", libelle="Immeuble Riad Salam", entite="BBR", client=6, type="immeuble", ville="Fès", adresse="45, avenue Hassan II", age=12, devis="envoye",
         bet=1, bct=None, archi=None, labo=None, description="Immeuble R+4 avec commerces au rez-de-chaussée.",
         dalles=[("A", "1", 0, 640.0), ("A", "1", 1, 610.0), ("A", "1", 2, 610.0)], cibles=[0, 0, 0]),
    dict(cle="oasis", libelle="Centre commercial Oasis", entite="APT", client=9, type="immeuble", ville="Casablanca", adresse="Route d'El Jadida, km 7", age=45, devis="version2",
         bet=0, bct=None, archi=0, labo=None, description="Galerie commerciale sur parking, trame de 12 m.",
         dalles=[("P", None, -1, 5200.0), ("G", None, 0, 4800.0)], cibles=[0, 0]),
    dict(cle="bouazza", libelle="Villa Dar Bouazza", entite="APT", client=8, type="villa", ville="Dar Bouazza", adresse="Lotissement Les Dunes, villa 12", age=5, devis="brouillon",
         bet=None, bct=None, archi=None, labo=None, description="Villa de plain-pied avec toiture-terrasse.",
         dalles=[(None, None, 0, 295.0)], cibles=[0]),
]


class Demo:
    def __init__(self):
        self.s = {}
        self.dir = self.de("directeur")
        self.users = {u["login"]: u["id"] for u in self.dir.get("/users")}
        self.part = {}
        self.categories = [c["libelle"] for c in self.dir.get("/parametres/categories-depense")["categories"] if c["actif"]]

    def de(self, login):
        if login not in self.s: self.s[login] = Session(login)
        return self.s[login]

    # ----- référentiels
    def utilisateurs(self):
        nouveaux = [("Yasmine", "Cherkaoui", "ingenieur"), ("Anas", "Filali", "dessinateur"), ("Hamza", "Boukhari", "chef_chantier"),
                    ("Rim", "Sqalli", "agent_commercial"), ("Tarik", "Lamrani", "administrateur")]
        for prenom, nom, role in nouveaux:
            login = f"{prenom}.{nom}".lower()
            if login in self.users: continue
            u = self.dir.post("/users", {"prenom": prenom, "nom": nom, "email": f"{login}@progix.example", "role": role, "password": MDP})
            self.users[u["login"]] = u["id"]
        dire(f"  utilisateurs : {len(self.users)} comptes")

    def partenaires(self):
        self.clients = [self.dir.post("/partenaires", {"type": "client_final", "exonereTva": False, **c})["id"] for c in CLIENTS]
        for i, (type_, nom, ville, contact, fonction) in enumerate(PARTENAIRES):
            p = self.dir.post("/partenaires", {"type": type_, "nature": "morale", "raisonSociale": nom, "ville": ville, "adresse": f"{10 + i * 7}, rue des Entrepreneurs",
                                               "telephone": f"05{22 + i} 40 {10 + i} {20 + i}", "email": f"contact@partenaire{i + 1}.example", "exonereTva": False,
                                               "contacts": [{"nom": contact, "fonction": fonction, "telephone": f"0661 {10 + i} {30 + i} {50 + i}", "email": None}]})
            self.part.setdefault(type_, []).append(p["id"])
        dire(f"  partenaires : {len(CLIENTS)} clients, {len(PARTENAIRES)} bureaux, laboratoires et entreprises")

    def stock(self):
        log = self.de("responsable.logistique")
        for entite, initial in (("APT", 180000), ("BBR", 120000)):
            articles = log.get(f"/logistique/stock?entite={entite}")["articles"]
            log.put("/logistique/stock/articles", {"entite": entite, "code": "toron", "stockInitial": initial, "seuil": 40000})
            for i, a in enumerate(x for x in articles if x["code"] != "toron"):
                log.put("/logistique/stock/articles", {"entite": entite, "code": a["code"], "stockInitial": 6000 + 1500 * (i % 4), "seuil": 800})
            log.post("/logistique/stock/mouvements", {"entite": entite, "code": "toron", "sens": "entree", "quantite": 60000, "motif": "Réception de bobines, bon fournisseur fictif 2026-118"})
            log.post("/logistique/stock/mouvements", {"entite": entite, "code": "toron", "sens": "correction", "quantite": -350, "motif": "Écart d'inventaire trimestriel"})
        dire("  stock : articles et mouvements des deux entités")

    # ----- projets
    def projet(self, d):
        cp = self.de("chef.projet")
        # Le commercial se choisit sur le projet ; ses devis le reprennent.
        commercial = self.users[["agent.commercial", "rim.sqalli"][PROJETS.index(d) % 2]]
        p = cp.post("/projets", {"libelle": d["libelle"], "clientId": self.clients[d["client"]], "entite": d["entite"], "typeConstruction": d["type"],
                                 "description": d["description"], "adresse": d["adresse"], "ville": d["ville"], "dateCreation": jour(d["age"]), "commercialId": commercial})
        d["id"] = p["id"]; d["code"] = p["code"]
        d["dalleIds"] = [cp.post(f"/projets/{p['id']}/dalles", {"bloc": b, "immeuble": im, "niveau": n, "detail": None, "surface": s})["id"] for b, im, n, s in d["dalles"]]
        for role, cle in (("bet", "bet"), ("bct", "bct"), ("architecte", "archi"), ("laboratoire", "labo")):
            if d.get(cle) is not None: cp.put(f"/projets/{p['id']}/partenaires/{role}", {"partenaireId": self.part[role][d[cle]]})
        return p

    def equipe(self, d, i):
        cp = self.de("chef.projet")
        ing = ["ingenieur", "yasmine.cherkaoui", "ingenieur.chef"][i % 3]
        chef = ["chef.chantier", "hamza.boukhari"][i % 2]
        cp.post(f"/projets/{d['id']}/equipe", {"fonction": "ingenieur", "userId": self.users[ing]})
        cp.post(f"/projets/{d['id']}/equipe", {"fonction": "chef_chantier", "userId": self.users[chef]})
        d["chef"] = chef
        for j, did in enumerate(d["dalleIds"]):
            self.de("dessinateur.chef").put(f"/projets/{d['id']}/dalles/{did}/dessinateur", {"userId": self.users[["dessinateur", "anas.filali"][(i + j) % 2]]})
            if d["cibles"][j] == 0:
                self.de("dessinateur.chef").put(f"/projets/{d['id']}/dalles/{did}/etude", {"date": (AUJ + timedelta(days=4 * j - 3)).isoformat()})

    # ----- devis
    def devis(self, d, i):
        # L'administrateur consulte les devis sans les établir ; la signature revient au responsable administratif.
        adm = self.de(["responsable.administratif", "ingenieur.chef", "chef.projet"][i % 3])
        sig = self.de("responsable.administratif")
        dv = adm.post("/devis", {"projetId": d["id"]})
        age = max(d["age"] - 3, 0)
        corps = {"dateDevis": jour(age), "dureeValidite": age + 30, "ingenieurId": self.users[["ingenieur", "yasmine.cherkaoui", "ingenieur.chef"][i % 3]],
                 "remiseM2": [0, 3, 5, 0][i % 4], "remiseMotif": [None, "Volume du projet", "Client fidèle", None][i % 4],
                 "echeances": dv["echeances"], "modalitesContrat": dv["modalitesContrat"],
                 "lignes": [{"id": l["id"], "surface": l["surface"], "ratioActif": round(3.6 + 0.2 * (k % 4), 2), "ratioPassif": round(7.5 + 0.5 * (k % 3), 2),
                             "systemes": [{"torons": 4, "ancrages": 2 * (20 + 3 * k), "gaine": round(l["surface"] * 0.9, 1)},
                                          {"torons": 7, "ancrages": 2 * (8 + k), "gaine": round(l["surface"] * 0.35, 1)}]} for k, l in enumerate(dv["lignes"])]}
        etat = d.get("devis", "valide")
        dv = adm.put(f"/devis/{dv['id']}", corps)
        if etat == "brouillon": return dv
        adm.post(f"/devis/{dv['id']}/envoyer")
        sig.post(f"/devis/{dv['id']}/signer", {"motDePasse": MDP})
        if etat == "version2":
            v2 = adm.post(f"/devis/{dv['id']}/nouvelle-version")
            corps["remiseM2"], corps["remiseMotif"], corps["dateDevis"], corps["lignes"] = 6, "Négociation après la première offre", jour(20), [
                {**l, "id": n["id"]} for l, n in zip(corps["lignes"], v2["lignes"])]
            adm.put(f"/devis/{v2['id']}", corps)
            adm.post(f"/devis/{v2['id']}/envoyer"); sig.post(f"/devis/{v2['id']}/signer", {"motDePasse": MDP})
            return v2
        if etat == "valide":
            dv = self.de("chef.projet").post(f"/devis/{dv['id']}/valider")
            d["devisId"] = dv["id"]
        return dv

    def contrat(self, d):
        ra = self.de("responsable.administratif")
        c = ra.post(f"/projets/{d['id']}/contrats", {"devisId": d["devisId"], "dateContrat": jour(max(d["age"] - 7, 0)), "lieu": "Casablanca"})
        ra.post(f"/contrats/{c['id']}/signer", {"motDePasse": MDP})

    # ----- suivi de chantier
    def avancer(self, d, j, cible):
        """Mène une dalle jusqu'à l'étape voulue, chaque geste étant fait par le rôle qui en a la charge."""
        pid, did = d["id"], d["dalleIds"][j]
        base = f"/projets/{pid}/dalles/{did}"
        fin = 4 if cible == "4v" else cible
        chantier, ing, log = self.de(d["chef"]), self.de("ingenieur"), self.de("responsable.logistique")
        resp, ra, cp = self.de("responsable.chantier"), self.de("responsable.administratif"), self.de("chef.projet")
        ref = f"{d['code']}-{j + 1:02d}"

        def valider(n):
            cp.post(f"{base}/etapes/{n}/valider", {"type": "technique"})
            ra.post(f"{base}/etapes/{n}/valider", {"type": "administrative"})
            if n in (9, 10): resp.post(f"{base}/etapes/{n}/valider", {"type": "chantier"})

        def photo(n, qui):
            qui.fichier("photo_etape", did, f"photo-etape-{n}-{ref}.png", png(90 + 15 * n, 120 + 10 * j, 150), "image/png", str(n))

        for n in range(1, fin + 1):
            if n == 1:
                des = self.de(["dessinateur", "anas.filali"][j % 2])
                v = des.post(f"{base}/versions", {"commentaire": "Première émission"})
                vid = v["versions"][0]["id"]
                des.fichier("plan_execution", vid, f"plan-execution-{ref}-A.pdf", pdf(f"Plan d'execution {ref} indice A"), "application/pdf", "plan")
                des.fichier("plan_execution", vid, f"modele-calcul-{ref}.pdf", pdf(f"Modele de calcul {ref}"), "application/pdf", "modele")
                if j % 3 == 0:
                    v = des.post(f"{base}/versions", {"commentaire": "Reprise après remarques du BET : réservations déplacées"})
                    vid = v["versions"][0]["id"]
                    des.fichier("plan_execution", vid, f"plan-execution-{ref}-B.pdf", pdf(f"Plan d'execution {ref} indice B"), "application/pdf", "plan")
                    des.fichier("plan_execution", vid, f"modele-calcul-{ref}-B.pdf", pdf(f"Modele de calcul {ref} indice B"), "application/pdf", "modele")
                des.post(f"{base}/etapes/1/realiser", {"remarque": None})
            elif n == 2:
                ing.post(f"{base}/etapes/2/realiser", {"remarque": "Avis favorable du bureau de contrôle, sans réserve." if j % 2 == 0 else "Avis favorable avec observation sur l'enrobage en rive."})
            elif n == 3:
                photo(3, chantier); chantier.post(f"{base}/etapes/3/realiser", {"remarque": None})
            elif n == 4:
                surface = d["dalles"][j][3]
                nb = max(12, int(surface / 28))
                acc = [a for a in log.get(f"{base}/commande")["proposes"]][:6]
                tendons, num = [], 100 * (j + 1)
                for sens, torons, nombre, longueur in (("X", 4, nb, 18.5 + 1.5 * (j % 3)), ("Y", 4, int(nb * 0.7), 24.0 + 2.0 * (j % 2)), ("Y", 7, 6, 31.5)):
                    for k in range(nombre):
                        num += 1
                        tendons.append({"numero": str(num), "torons": torons, "sens": sens, "longueur": round(longueur + 0.07 * (k % 9), 2), "ancrages": 2})
                log.put(f"{base}/commande", {"tendons": tendons, "surface": surface,
                    "accessoires": [{"libelle": a["libelle"], "quantite": 4 + 3 * k, "unite": a.get("unite")} for k, a in enumerate(acc)]})
                log.post(f"{base}/etapes/4/realiser", {"remarque": None})
                if cible == "4v": return
                valider(4)
                etats = ["fabrication", "livree", "recue"] if fin >= 5 else ["fabrication"]
                for e in etats: log.post(f"/logistique/commandes/{did}/etat", {"etat": e})
            elif n == 5:
                photo(5, chantier); chantier.post(f"{base}/etapes/5/realiser", {"remarque": None})
            elif n == 6:
                points = resp.get(f"{base}/auto-controle")["points"]
                resp.put(f"{base}/auto-controle", {"points": [{"code": p["code"], "resultat": "nc" if (k, j) == (2, 1) else "na" if k == 6 else "c",
                    "observation": "Recouvrement repris sur deux nappes avant coulage." if (k, j) == (2, 1) else None} for k, p in enumerate(points)]})
                photo(6, chantier)
                chantier.post(f"{base}/etapes/6/realiser", {"remarque": None})
            elif n == 7:
                photo(7, chantier); chantier.post(f"{base}/etapes/7/realiser", {"remarque": "Coulage en une seule phase, béton pompé."})
            elif n == 8:
                if j == 1:
                    # Un premier rapport non conforme, gardé dans l'historique, puis un second conforme.
                    r = ing.post(f"{base}/rapports-ecrasement", {"valeur": 19.8, "conforme": False, "remarque": "Résistance insuffisante à 3 jours : nouvel essai demandé."})
                    ing.fichier("rapport_ecrasement", r["rapports"][0]["id"], f"rapport-ecrasement-{ref}-1.pdf", pdf(f"Rapport d'ecrasement {ref} : 19,8 MPa a 3 jours"), "application/pdf")
                r = ing.post(f"{base}/rapports-ecrasement", {"valeur": round(26.5 + 0.6 * j, 1), "conforme": True, "remarque": None})
                ing.fichier("rapport_ecrasement", r["rapports"][0]["id"], f"rapport-ecrasement-{ref}.pdf", pdf(f"Rapport d'ecrasement {ref}"), "application/pdf")
                ing.post(f"{base}/etapes/8/realiser", {"remarque": "Résistance atteinte à 4 jours : mise en tension autorisée."})
            elif n == 9:
                valider(9)
                cables = chantier.get(f"{base}/tension")["cables"]
                mesures = []
                for k, c in enumerate(cables):
                    th = c["theorique"]; total = th * (0.98 + 0.01 * (k % 7))
                    un_cote = c["longueur"] is not None and c["longueur"] < 20 and k % 3 == 0
                    a = round(total if un_cote else total * 0.52, 0)
                    a25 = 30 + (k % 5)
                    m = {"cable": c["cable"], "longueur": c["longueur"], "theorique": th, "a25": a25, "a100": a25 + a, "b25": None, "b100": None}
                    if not un_cote: m["b25"] = 28 + (k % 4); m["b100"] = m["b25"] + round(total - a, 0)
                    mesures.append(m)
                chantier.put(f"{base}/tension", {"cables": mesures, "forceKn": 195, "cr": "CR-0" + str(2 + j % 3), "cmp": "CMP-1" + str(4 + j % 4)})
                chantier.post(f"{base}/etapes/9/realiser", {"remarque": "Mise en tension réalisée sans incident, allongements conformes."})
            elif n == 10:
                valider(10)
                fi = chantier.get(f"{base}/injection")
                signature = "data:image/png;base64," + __import__("base64").b64encode(png(245, 245, 245, 60)).decode()
                chantier.put(f"{base}/injection", {"airAmbiant": 24.5, "malaxeur": "Malaxeur MX-200", "pompe": "Pompe PH-15",
                    "cimentType": fi.get("cimentType") or "CPJ 45", "cimentKg": fi.get("cimentKg") or 50, "cimentTemperature": 22, "eau": fi.get("eau") or "Eau potable",
                    "eauLitres": fi.get("eauLitres") or 19, "adjuvantType": fi.get("adjuvantType") or "Fluidifiant expansif", "adjuvantGrammes": fi.get("adjuvantGrammes") or 500,
                    "cables": [{"cable": c["cable"], "testAir": True, "fluidite": 13 + (k % 4), "temperature": 23 + (k % 3), "pression": 5 + 0.5 * (k % 3),
                                "depart": f"{9 + k // 12:02d}:{(k * 5) % 60:02d}", "fin": f"{9 + k // 12:02d}:{(k * 5) % 60 + 4:02d}"} for k, c in enumerate(fi["cables"])],
                    "clientNom": "Représentant du maître d'ouvrage", "clientSignature": signature})
                chantier.post(f"{base}/etapes/10/realiser", {"remarque": None, "motDePasse": MDP})
            elif n == 11:
                resp.post(f"{base}/etapes/11/realiser", {"remarque": "Réception sans réserve."})

    # ----- factures et règlements
    def regler(self, facture, montant, mode, age, session="responsable.administratif", echeance=None):
        """Paiement sur le projet, ventilé entre les dalles de la facture (ou toutes les dalles pour un acompte) au prorata de leur reste."""
        refs = {"cheque": "CHQ 7745" + str(age).zfill(3), "virement": "VIR 2026-" + str(4000 + age), "effet": "LCN 5521" + str(age).zfill(3), "especes": None}
        s = self.de(session)
        vue = s.get(f"/projets/{facture['projetId']}/paiements")
        dalles = None if facture.get("acompte") else {l["libelle"].split(" · ")[0] for l in s.get(f"/factures/{facture['id']}")["lignes"]}
        ouvertes = [x for x in vue["dalles"] if x["aCouvrir"] > 0 and (dalles is None or x["reference"] in dalles)]
        total = sum(x["aCouvrir"] for x in ouvertes)
        if total <= 0: return
        montant, parts, cumul = min(montant, total), [], 0
        for i, x in enumerate(ouvertes):
            part = round(montant - cumul, 2) if i == len(ouvertes) - 1 else round(montant * x["aCouvrir"] / total, 2)
            cumul += part
            parts.append({"dalleId": x["dalleId"], "montant": min(part, x["aCouvrir"])})
        s.post(f"/projets/{facture['projetId']}/paiements", {"datePaiement": jour(age), "mode": mode, "reference": refs[mode], "dalles": parts,
            "banque": None if mode == "especes" else ["Attijariwafa bank", "Banque Populaire", "BMCE Bank of Africa", "CIH Bank"][age % 4], "echeanceEffet": echeance, "motDePasse": MDP})

    def acompte(self, d, ttc, age, mode="virement"):
        """Plus de facture d'acompte : le paiement à la signature se saisit directement sur les dalles (voir regler)."""
        return {"projetId": d["id"], "totalTTC": ttc, "acompte": True}

    def livrer(self, d, indices, age):
        log = self.de("administrateur")
        b = log.post("/livraisons", {"projetId": d["id"], "dateLivraison": jour(age), "dalleIds": [d["dalleIds"][k] for k in indices]})
        log.post(f"/livraisons/{b['id']}/signer", {"motDePasse": MDP})
        return b

    def facturer(self, d, bons, age, delai=30, mode="virement", session="responsable.administratif"):
        a = self.de(session)
        f = a.post("/factures", {"projetId": d["id"], "type": "livraison", "bonIds": [b["id"] for b in bons], "dateFacture": jour(age), "echeance": jour(age - delai), "modeReglement": mode})
        a.post(f"/factures/{f['id']}/signer", {"motDePasse": MDP})
        return f

    def avoir(self, f, age, motif, ht=None):
        ra = self.de("responsable.administratif")
        a = ra.post(f"/factures/{f['id']}/avoirs", {"total": ht is None, "montantHT": ht, "dateAvoir": jour(age), "motif": motif})
        ra.post(f"/avoirs/{a['avoirs'][-1]['id']}/signer", {"motDePasse": MDP})
        return a

    def finances(self):
        """Factures, règlements et avoirs, joués dans l'ordre des dates pour que les numéros se suivent."""
        P = {d["cle"]: d for d in PROJETS}
        ev, m = [], {}
        def a(age, fn): ev.append((age, len(ev), fn))

        d = P["anfa"]; t = d["ttc"]
        a(142, lambda d=d, t=t: m.__setitem__("anfa0", self.acompte(d, round(t * 0.30, 2), 142)))
        a(138, lambda: self.regler(m["anfa0"], m["anfa0"]["totalTTC"], "virement", 138))
        a(110, lambda d=d: m.__setitem__("anfaB1", self.livrer(d, [0, 1], 110)))
        a(108, lambda d=d: m.__setitem__("anfa1", self.facturer(d, [m["anfaB1"]], 108)))
        a(95, lambda: self.regler(m["anfa1"], m["anfa1"]["totalTTC"], "cheque", 95, "administrateur"))
        a(80, lambda d=d: m.__setitem__("anfaB2", self.livrer(d, [2, 3], 80)))
        a(78, lambda d=d: m.__setitem__("anfa2", self.facturer(d, [m["anfaB2"]], 78, mode="cheque")))
        a(60, lambda: self.regler(m["anfa2"], m["anfa2"]["totalTTC"], "virement", 60))
        a(55, lambda: self.avoir(m["anfa2"], 55, "Geste commercial : reprise d'une réservation oubliée au 3e étage", 5000))
        a(50, lambda: self.de("responsable.administratif").post(f"/factures/{m['anfa2']['id']}/remboursements", {"datePaiement": jour(50), "mode": "virement",
              "montant": 6000, "reference": "VIR RMB 2026-031", "banque": "Attijariwafa bank", "motDePasse": MDP}))
        a(50, lambda d=d: m.__setitem__("anfaB3", self.livrer(d, [4], 50)))
        a(48, lambda d=d: m.__setitem__("anfa3", self.facturer(d, [m["anfaB3"]], 48)))
        a(30, lambda: self.regler(m["anfa3"], round(m["anfa3"]["totalTTC"] * 0.6, 2), "virement", 30))
        a(6, lambda: self.regler(m["anfa3"], round(m["anfa3"]["totalTTC"] * 0.4 - 1, 2), "effet", 6, echeance=(AUJ + timedelta(days=40)).isoformat()))

        d = P["golf"]
        a(114, lambda d=d: m.__setitem__("golf0", self.acompte(d, 40000, 114, "cheque")))
        a(112, lambda: self.regler(m["golf0"], 5000, "especes", 112, "administrateur"))
        a(111, lambda: self.regler(m["golf0"], 35000, "cheque", 111, "administrateur"))
        a(70, lambda d=d: m.__setitem__("golfB", self.livrer(d, [0, 1], 70)))
        a(68, lambda d=d: m.__setitem__("golf1", self.facturer(d, [m["golfB"]], 68, mode="cheque")))
        a(62, lambda: self.avoir(m["golf1"], 62, "Remise accordée après retard de livraison de la dalle haute", 3000))
        a(40, lambda: self.regler(m["golf1"], round(m["golf1"]["totalTTC"] - 3600, 2), "cheque", 40))

        d = P["marina"]; t = d["ttc"]
        a(92, lambda d=d, t=t: m.__setitem__("mar0", self.acompte(d, round(t * 0.30, 2), 92)))
        a(88, lambda: self.regler(m["mar0"], m["mar0"]["totalTTC"], "virement", 88))
        a(55, lambda d=d: m.__setitem__("marB1", self.livrer(d, [0, 1], 55)))
        a(53, lambda d=d: m.__setitem__("mar1", self.facturer(d, [m["marB1"]], 53, mode="effet")))
        a(45, lambda: self.regler(m["mar1"], round(m["mar1"]["totalTTC"] / 2, 2), "effet", 45, echeance=jour(10)))
        a(44, lambda: self.regler(m["mar1"], round(m["mar1"]["totalTTC"] / 2 - 1, 2), "effet", 44, echeance=(AUJ + timedelta(days=20)).isoformat()))
        a(25, lambda d=d: m.__setitem__("marB2", self.livrer(d, [2], 25)))
        a(23, lambda d=d: m.__setitem__("mar2", self.facturer(d, [m["marB2"]], 23, delai=15)))
        a(10, lambda d=d: m.__setitem__("marB3", self.livrer(d, [3], 10)))
        a(3, lambda d=d: m.__setitem__("mar3", self.facturer(d, [m["marB3"]], 3)))
        a(1, lambda: self.regler(m["mar3"], round(m["mar3"]["totalTTC"] * 0.5, 2), "virement", 1))

        d = P["clinique"]; t = d["ttc"]
        a(72, lambda d=d, t=t: m.__setitem__("cli0", self.acompte(d, round(t * 0.20, 2), 72)))
        a(70, lambda: self.regler(m["cli0"], m["cli0"]["totalTTC"], "virement", 70))
        a(30, lambda d=d: m.__setitem__("cliB1", self.livrer(d, [0, 1], 30)))
        a(28, lambda d=d: m.__setitem__("cli1", self.facturer(d, [m["cliB1"]], 28, delai=20, session="administrateur")))
        a(22, lambda d=d: m.__setitem__("cliB2", self.livrer(d, [2], 22)))
        a(21, lambda d=d: m.__setitem__("cli2", self.facturer(d, [m["cliB2"]], 21)))
        a(20, lambda: self.avoir(m["cli2"], 20, "Facture émise par erreur : la dalle du 2e étage n'était pas encore livrée en totalité"))

        d = P["logis"]; t = d["ttc"]
        a(52, lambda d=d, t=t: m.__setitem__("log0", self.acompte(d, round(t * 0.25, 2), 52, "cheque")))
        a(47, lambda: self.regler(m["log0"], round(m["log0"]["totalTTC"] * 0.5, 2), "cheque", 47))
        a(15, lambda d=d: m.__setitem__("logB", self.livrer(d, [0], 15)))
        a(2, lambda d=d: m.__setitem__("log1", self.facturer(d, [m["logB"]], 2, mode="cheque", session="administrateur")))
        a(0, lambda: self.regler(m["log1"], 60000, "cheque", 0, "administrateur"))

        d = P["agdal"]; t = d["ttc"]
        a(33, lambda d=d, t=t: m.__setitem__("agd0", self.acompte(d, round(t * 0.30, 2), 33)))

        d = P["massira"]
        a(18, lambda d=d: m.__setitem__("mas0", self.acompte(d, 250000, 18)))
        a(12, lambda: self.regler(m["mas0"], 250000, "virement", 12))

        for _, _, fn in sorted(ev, key=lambda x: (-x[0], x[1])): fn()
        dire(f"  {len(ev)} opérations : bons de livraison, factures, règlements, avoirs, remboursement")

    def depenses(self):
        n = 0
        for d in PROJETS:
            for j, cible in enumerate(d["cibles"]):
                if not isinstance(cible, int) or cible < 5: continue
                surface, did = d["dalles"][j][3], d["dalleIds"][j]
                age = max(d["age"] - 35 - 12 * j, 3)
                lignes = [(self.categories[0], surface * 31, "virement", "Torons et ancrages, facture fournisseur fictive", age + 6, "administrateur"),
                          (self.categories[1 % len(self.categories)], surface * 9.5, "virement", "Équipe de pose, quinzaine", age, "responsable.chantier"),
                          (self.categories[2 % len(self.categories)], 1800 + 350 * j, "especes", "Transport des bobines et du matériel de mise en tension", age + 2, d["chef"])]
                for cat, montant, mode, libelle, quand, qui in lignes:
                    self.de(qui).post(f"/projets/{d['id']}/depenses", {"dalleId": did, "categorie": cat, "dateDepense": jour(quand), "montant": round(montant, 2),
                                                                       "mode": mode, "reference": None if mode == "especes" else f"VIR F-{1000 + n}", "libelle": libelle})
                    n += 1
        ra = self.de("responsable.administratif")
        generales = [("Loyer du dépôt et des bureaux", 18500, "virement"), ("Salaires du personnel administratif", 64000, "virement"), ("Carburant et péages", 4300, "especes"),
                     ("Électricité et eau", 3150, "cheque"), ("Entretien des vérins et des pompes", 7800, "cheque")]
        for mois in range(5):
            for k, (libelle, montant, mode) in enumerate(generales):
                if k == 4 and mois % 2: continue
                entite = "APT" if (k + mois) % 2 == 0 else "BBR"
                x = ra.post("/depenses", {"entite": entite, "dateDepense": jour(mois * 30 + 2 + k), "libelle": libelle, "montant": montant + 120 * mois, "mode": mode,
                                          "reference": None if mode == "especes" else f"{'CHQ' if mode == 'cheque' else 'VIR'} G-{200 + n}"})
                n += 1
        ra.post(f"/depenses/{x['id']}/annuler", {"motif": "Saisie en double"})
        dire(f"  {n} dépenses, de dalle et générales")

    def courriers(self):
        ing, n = self.de("ingenieur"), 0
        for d in PROJETS:
            faits = [(j, c) for j, c in enumerate(d["cibles"]) if c]
            if not faits: continue
            lignes = [{"dalleId": d["dalleIds"][j], "numeroPlan": f"PT-{j + 1:02d}{'B' if j % 3 == 0 else 'A'}", "designation": "Plan de câblage et de ferraillage passif", "nombre": 3} for j, _ in faits]
            for type_, cle, age in (("bordereau_bet", "bet", 6), ("bordereau_bct", "bct", 8)):
                if d.get(cle) is None: continue
                c = ing.post(f"/projets/{d['id']}/courriers", {"type": type_, "dateCourrier": jour(max(d["age"] - age, 0)), "lieu": "Casablanca",
                    "objet": "Transmission des plans d'exécution de post-tension", "entreprise": None, "modification": False, "lignes": lignes})
                ing.post(f"/courriers/{c['id']}/signer", {"motDePasse": MDP}); n += 1
        d = next(x for x in PROJETS if x["cle"] == "marina")
        c = ing.post(f"/projets/{d['id']}/courriers", {"type": "bordereau_bct", "dateCourrier": jour(30), "lieu": "Tanger", "objet": "Plans modifiés après remarques",
            "entreprise": None, "modification": True, "lignes": [{"dalleId": d["dalleIds"][0], "numeroPlan": "PT-01C", "designation": "Plan de câblage modifié", "nombre": 2}]})
        self.de("chef.projet").post(f"/courriers/{c['id']}/annuler", {"motif": "Mauvais indice de plan joint au bordereau"})
        # Conformité des dalles réalisées, puis attestations.
        cp = self.de("chef.projet")
        for d in PROJETS:
            finies = [j for j, c in enumerate(d["cibles"]) if c == 11]
            conformes = []
            for j in finies:
                ok = not (d["cle"] == "golf" and j == 1)
                cp.put(f"/projets/{d['id']}/dalles/{d['dalleIds'][j]}/conformite", {"conforme": ok, "remarque": None if ok else "Flèche à surveiller en rive : contrôle complémentaire demandé."})
                if ok: conformes.append(j)
            if conformes and d["cle"] != "marina":
                c = cp.post(f"/projets/{d['id']}/courriers", {"type": "attestation", "dateCourrier": jour(15), "lieu": "Casablanca", "objet": None, "entreprise": None,
                    "modification": False, "lignes": [{"dalleId": d["dalleIds"][j], "numeroPlan": None, "designation": None, "nombre": 1} for j in conformes]})
                cp.post(f"/courriers/{c['id']}/signer", {"motDePasse": MDP}); n += 1
        dire(f"  {n + 1} courriers : bordereaux et attestations de conformité")

    def divers(self):
        P = {d["cle"]: d for d in PROJETS}
        notes = [("anfa", "chef.projet", "Réception finale prononcée, levée des réserves faite avec le maître d'ouvrage."),
                 ("marina", "ingenieur", "Le BET demande une note de calcul complémentaire pour le plancher haut du 3e étage."),
                 ("marina", "responsable.chantier", "Grue indisponible deux jours la semaine prochaine : pose décalée."),
                 ("clinique", "responsable.administratif", "Attestation d'exonération de TVA reçue et vérifiée."),
                 ("logis", "responsable.logistique", "Commande usine de la zone 2 en attente de validation depuis lundi."),
                 ("agdal", "chef.projet", "Relance du client pour l'acompte, sans réponse à ce jour."),
                 ("riad", "agent.commercial", "Le client compare avec une solution en dalle pleine, décision attendue sous quinzaine.")]
        for cle, qui, texte in notes: self.de(qui).post(f"/projets/{P[cle]['id']}/remarques", {"texte": texte})
        self.dir.post(f"/projets/{P['agdal']['id']}/bloquer", {"motif": "Acompte impayé malgré deux relances"})
        dire("  remarques de suivi, et un projet bloqué pour impayé")

    def lancer(self):
        dire("Référentiels")
        self.utilisateurs(); self.partenaires(); self.stock()
        dire("Projets")
        for d in PROJETS:
            self.projet(d)
            self.equipe(d, PROJETS.index(d))
            dv = self.devis(d, PROJETS.index(d))
            dv = self.dir.get(f"/devis/{dv['id']}")
            dire(f"  {d['code']}  {d['libelle']} : devis {dv['numero']} v{dv['version']}, {dv['etatLabel'].lower()}, {dh(dv['totalTTC'])} TTC")
            d["ttc"] = dv["totalTTC"]
            if "devisId" not in d: continue
            self.contrat(d)
            for j, cible in enumerate(d["cibles"]):
                if cible: self.avancer(d, j, cible)
            dire("      dalles : " + ", ".join(str(c) for c in d["cibles"]) + " étapes faites")


def suite(demo):
    dire("Factures et règlements"); demo.finances()
    dire("Dépenses"); demo.depenses()
    dire("Courriers"); demo.courriers()
    dire("Suivi"); demo.divers()


def main():
    dire(f"PROGIX, données fictives -> {API}")
    try:
        demo = Demo()
    except Exception as e:
        sys.exit(f"Connexion impossible à {API} avec le compte directeur : {e}")
    if demo.dir.get("/projets?q=" + urllib.request.quote(PROJETS[0]["libelle"]))["total"] > 0:
        sys.exit("Les données fictives sont déjà présentes : rien n'a été ajouté.")
    demo.lancer()
    suite(demo)
    dire("Terminé. Connexion : directeur, ou tout autre compte, avec le mot de passe de démonstration.")


if __name__ == "__main__":
    main()
