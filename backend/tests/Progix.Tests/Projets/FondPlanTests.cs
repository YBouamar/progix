using NetTopologySuite.Geometries;
using Progix.Modules.Projets.Application;
using Progix.Modules.Projets.Infrastructure;

namespace Progix.Tests.Projets;

/// <summary>Fond de plan tiré d'un plan de coffrage : rôles des calques, niveaux, dalle, poteaux, voiles et réservations.</summary>
public class FondPlanTests
{
    private static Polygon Rect(double x, double y, double l, double h) =>
        FondPlan.Polygone([(x, y), (x + l, y), (x + l, y + h), (x, y + h)], fermee: true)!;

    /// <summary>Un étage de 20 m par 12 m : quatre poteaux, un voile en deux morceaux, une trémie, un titre.</summary>
    private static PlanCoffrage Etage(double ox = 0)
    {
        var plan = new PlanCoffrage();
        plan.Contours.Add(new ContourPlan("coffrage", Rect(ox, 0, 20, 12)));
        foreach (var (x, y) in new[] { (1.0, 1.0), (18.7, 1.0), (1.0, 10.7), (18.7, 10.7) }) plan.Contours.Add(new ContourPlan("1-poteaux", Rect(ox + x, y, 0.3, 0.3)));
        plan.Contours.Add(new ContourPlan("VOILE", Rect(ox + 6, 6, 2, 0.2)));
        plan.Contours.Add(new ContourPlan("VOILE", Rect(ox + 8, 6, 3, 0.2)));
        plan.Contours.Add(new ContourPlan("Vide", Rect(ox + 12, 3, 2, 1.5)));
        plan.Contours.Add(new ContourPlan("Cotation", Rect(ox + 3, 3, 1, 1)));
        plan.Titres.Add(new TitrePlan(ox + 9, -3, "PH ETAGE", 0.6));
        return plan;
    }

    [Theory]
    [InlineData("1-poteaux", FondPlan.Poteaux)]
    [InlineData("L01-BA-POTEAU", FondPlan.Poteaux)]
    [InlineData("00-BETB-POTAUX", FondPlan.Poteaux)]
    [InlineData("Voile contreventement", FondPlan.Voiles)]
    [InlineData("L00-BA-HACHURE POT.V", FondPlan.Voiles)]
    [InlineData("L03-BA-DALLE", FondPlan.Dalle)]
    [InlineData("-0-limites des dalles mise a jour", FondPlan.Dalle)]
    [InlineData("BET-coffrage", FondPlan.Dalle)]
    [InlineData("poutre", FondPlan.Reservations)]
    [InlineData("Cotation", FondPlan.Ignore)]
    public void Role_propose_d_apres_le_nom_du_calque(string calque, string attendu) => Assert.Equal(attendu, FondPlan.RolePropose(calque));

    [Fact]
    public void Un_etage_simple_donne_sa_dalle_ses_poteaux_son_voile_et_sa_tremie()
    {
        var plan = Etage();
        var plans = FondPlan.Plans(plan, FondPlan.RolesProposes(plan));
        var (niveau, fond) = Assert.Single(plans);
        Assert.Equal("PH ETAGE", niveau.Nom);
        Assert.Equal(240, fond.Dalle.Area, 1);
        Assert.Equal(4, fond.Poteaux.Count);
        // Les deux morceaux alignés bout à bout font un seul voile de 5 m.
        var voile = Assert.Single(fond.Voiles);
        Assert.Equal(1.0, voile.Area, 2);
        Assert.Equal(3.0, Assert.Single(fond.Reservations).Area, 2);
    }

    [Fact]
    public void Deux_plans_cote_a_cote_donnent_deux_niveaux_de_gauche_a_droite()
    {
        var plan = Etage();
        var second = Etage(60);
        plan.Contours.AddRange(second.Contours);
        plan.Titres.Add(new TitrePlan(69, -3, "PH RDC", 0.6));
        plan.Titres.RemoveAll(t => t.Texte == "PH ETAGE" && t.X > 30);
        var plans = FondPlan.Plans(plan, FondPlan.RolesProposes(plan));
        Assert.Equal(["PH ETAGE", "PH RDC"], plans.Select(p => p.Niveau.Nom));
    }

    [Fact]
    public void Un_contour_sans_poteau_ni_voile_n_est_pas_un_niveau()
    {
        var plan = new PlanCoffrage();
        plan.Contours.Add(new ContourPlan("coffrage", Rect(0, 0, 30, 20)));
        Assert.Empty(FondPlan.Plans(plan, FondPlan.RolesProposes(plan)));
    }

    [Fact]
    public void Le_role_corrige_par_l_ingenieur_remplace_la_proposition()
    {
        var plan = Etage();
        var roles = FondPlan.RolesProposes(plan);
        // La trémie est sur un calque au nom sans rapport : ignorée d'abord, trouvée une fois le calque affecté.
        plan.Contours.Add(new ContourPlan("Calque 12", Rect(15, 8, 1.5, 1.2)));
        roles = FondPlan.RolesProposes(plan);
        Assert.Single(FondPlan.Plans(plan, roles)[0].Fond.Reservations);
        roles["Calque 12"] = FondPlan.Reservations;
        Assert.Equal(2, FondPlan.Plans(plan, roles)[0].Fond.Reservations.Count);
        roles["1-poteaux"] = FondPlan.Ignore;
        Assert.Empty(FondPlan.Plans(plan, roles));
    }

    [Fact]
    public void Sur_le_calque_des_poteaux_une_forme_allongee_est_un_voile_et_une_cage_donne_ses_voiles_et_son_vide()
    {
        var plan = Etage();
        plan.Contours.Add(new ContourPlan("1-poteaux", Rect(4, 0, 1.0, 0.2)));
        // Cage de 2 m par 1,8 m, parois de 20 cm : contour extérieur et contour intérieur sur le calque des poteaux.
        plan.Contours.Add(new ContourPlan("1-poteaux", Rect(9, 2, 2, 1.8)));
        plan.Contours.Add(new ContourPlan("1-poteaux", Rect(9.2, 2.2, 1.6, 1.4)));
        var fond = FondPlan.Plans(plan, FondPlan.RolesProposes(plan))[0].Fond;
        Assert.Equal(4, fond.Poteaux.Count);
        Assert.Equal(1 + 1 + 4, fond.Voiles.Count);
        Assert.Contains(fond.Reservations, r => Math.Abs(r.Area - 2.24) < 0.01);
    }

    [Fact]
    public void Les_voiles_peripheriques_d_un_sous_sol_etendent_la_dalle_et_donnent_un_voile_par_cote()
    {
        var plan = new PlanCoffrage();
        // Deux enceintes accolées, chacune dessinée par son contour extérieur et son contour intérieur.
        plan.Contours.Add(new ContourPlan("VOILE", Rect(0, 0, 15, 12)));
        plan.Contours.Add(new ContourPlan("VOILE", Rect(0.2, 0.2, 14.6, 11.6)));
        plan.Contours.Add(new ContourPlan("VOILE", Rect(-4, 2, 4, 8)));
        plan.Contours.Add(new ContourPlan("VOILE", Rect(-3.8, 2.2, 3.6, 7.6)));
        plan.Contours.Add(new ContourPlan("1-poteaux", Rect(7, 6, 0.3, 0.3)));
        var fond = FondPlan.Plans(plan, FondPlan.RolesProposes(plan))[0].Fond;
        Assert.Equal(15 * 12 + 4 * 8, fond.Dalle.Area, 0);
        Assert.Equal(8, fond.Voiles.Count);
        Assert.Empty(fond.Reservations);
    }

    [Fact]
    public void Une_polyligne_ouverte_courte_n_est_pas_une_surface_un_long_contour_non_ferme_l_est()
    {
        Assert.Null(FondPlan.Polygone([(0, 0), (5, 0), (5, 3)], fermee: false));
        Assert.NotNull(FondPlan.Polygone([(0, 0), (5, 0), (5, 3)], fermee: true));
        Assert.NotNull(FondPlan.Polygone([(0, 0), (10, 0), (10, 4), (6, 4), (6, 8), (0, 8), (0, 2)], fermee: false));
    }

    [Fact]
    public void Le_fond_de_plan_ecrit_se_relit_avec_ses_quatre_calques_et_son_origine_a_zero()
    {
        var plan = Etage(100);
        plan.Ronds.Add(new RondPlan("1-poteaux", 110, 6, 0.2));
        var fond = FondPlan.Plans(plan, FondPlan.RolesProposes(plan))[0].Fond;
        using var sortie = new MemoryStream();
        PlanCoffrageDwg.Ecrire(fond, sortie);
        using var relu = new MemoryStream(sortie.ToArray());
        var copie = PlanCoffrageDwg.Lire(relu, "fond.dxf");
        Assert.Equal(1, copie.Contours.Count(c => c.Calque == PlanCoffrageDwg.CalqueDalle));
        Assert.Equal(4, copie.Contours.Count(c => c.Calque == PlanCoffrageDwg.CalquePoteaux));
        Assert.Equal(1, copie.Contours.Count(c => c.Calque == PlanCoffrageDwg.CalqueVoiles));
        Assert.Equal(1, copie.Contours.Count(c => c.Calque == PlanCoffrageDwg.CalqueReservations));
        var rond = Assert.Single(copie.Ronds);
        Assert.Equal((10.0, 6.0, 0.2), (Math.Round(rond.X, 3), Math.Round(rond.Y, 3), Math.Round(rond.Rayon, 3)));
        var dalle = copie.Contours.Single(c => c.Calque == PlanCoffrageDwg.CalqueDalle).Polygone.EnvelopeInternal;
        Assert.Equal((0.0, 0.0, 20.0, 12.0), (Math.Round(dalle.MinX, 3), Math.Round(dalle.MinY, 3), Math.Round(dalle.MaxX, 3), Math.Round(dalle.MaxY, 3)));
    }
}
