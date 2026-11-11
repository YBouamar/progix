using Progix.Modules.Projets.Domain;

namespace Progix.Tests.Projets;

/// <summary>Exécution des dalles : dossiers EXE et MODIF, états, clôture et partage entre dessinateurs.</summary>
public class ExecutionDalleTests
{
    [Fact]
    public void Le_premier_dossier_est_un_exe_les_suivants_des_modif()
    {
        Assert.Equal(ExecutionDalle.Exe, ExecutionDalle.Type(premier: true));
        Assert.Equal(ExecutionDalle.Modif, ExecutionDalle.Type(premier: false));
    }

    [Theory]
    [InlineData(null, 0, ExecutionDalle.AAffecter)]
    [InlineData(null, 2, ExecutionDalle.ALancer)]
    [InlineData(ExecutionDalle.EnCours, 1, ExecutionDalle.EnCours)]
    [InlineData(ExecutionDalle.Termine, 0, ExecutionDalle.Termine)]
    [InlineData(ExecutionDalle.Arrete, 1, ExecutionDalle.Arrete)]
    public void L_etat_d_une_dalle_suit_son_dernier_dossier_sinon_ses_dessinateurs(string? dossier, int dessinateurs, string attendu) =>
        Assert.Equal(attendu, ExecutionDalle.Etat(dossier, dessinateurs));

    [Fact]
    public void Un_dossier_ne_s_ouvre_pas_tant_que_le_precedent_est_en_cours()
    {
        Assert.Contains("dossier A est encore en cours", ExecutionDalle.RefusOuvrir(ExecutionDalle.EnCours, 'A', agit: true));
        Assert.Null(ExecutionDalle.RefusOuvrir(ExecutionDalle.Termine, 'A', agit: true));
        Assert.Null(ExecutionDalle.RefusOuvrir(ExecutionDalle.Arrete, 'A', agit: true));
        Assert.Null(ExecutionDalle.RefusOuvrir(null, null, agit: true));
        Assert.NotNull(ExecutionDalle.RefusOuvrir(null, null, agit: false));
    }

    [Fact]
    public void La_cloture_demande_un_dessinateur_une_surface_et_des_fichiers_complets()
    {
        Assert.Null(ExecutionDalle.RefusTerminer(ExecutionDalle.EnCours, agit: true, dessinateurs: 1, surface: 320m, manque: null));
        Assert.Contains("déjà terminé", ExecutionDalle.RefusTerminer(ExecutionDalle.Termine, true, 1, 320m, null));
        Assert.Contains("arrêté", ExecutionDalle.RefusTerminer(ExecutionDalle.Arrete, true, 1, 320m, null));
        Assert.Contains("affectés", ExecutionDalle.RefusTerminer(ExecutionDalle.EnCours, false, 1, 320m, null));
        Assert.Contains("au moins un dessinateur", ExecutionDalle.RefusTerminer(ExecutionDalle.EnCours, true, 0, 320m, null));
        Assert.Contains("surface", ExecutionDalle.RefusTerminer(ExecutionDalle.EnCours, true, 1, 0m, null));
        Assert.Equal("Il manque le plan.", ExecutionDalle.RefusTerminer(ExecutionDalle.EnCours, true, 1, 320m, "Il manque le plan."));
    }

    [Fact]
    public void Un_dossier_partage_se_divise_a_parts_egales()
    {
        Assert.Equal(1m, ExecutionDalle.Part(1));
        Assert.Equal(0.5m, ExecutionDalle.Part(2));
        Assert.Equal(0m, ExecutionDalle.Part(0));
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal([a, b], ExecutionDalle.Ids($"{a},{b}"));
        Assert.Empty(ExecutionDalle.Ids(null));
    }
}
