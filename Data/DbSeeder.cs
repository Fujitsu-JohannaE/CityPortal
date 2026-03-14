using CityPortal.Models;
using Microsoft.EntityFrameworkCore;

namespace CityPortal.Data;

/// <summary>
/// Seeds the SQL database with demo tenants, form definitions, fields, and submissions.
/// Runs once at startup when the database is empty.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        // Only seed if no tenants exist yet
        if (await db.Tenants.AnyAsync())
            return;

        var vantaaId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
        var espooId  = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

        // ── Tenants ──────────────────────────────────────────────────────────
        db.Tenants.AddRange(
            new Tenant { Id = vantaaId, Name = "Vantaan kaupunki", Slug = "vantaa" },
            new Tenant { Id = espooId,  Name = "Espoon kaupunki",  Slug = "espoo" }
        );

        // ── Vantaa forms ─────────────────────────────────────────────────────
        var vPotholeId = Guid.NewGuid();
        var vTreeId    = Guid.NewGuid();
        var vNoiseId   = Guid.NewGuid();

        db.FormDefinitions.AddRange(
            new FormDefinition
            {
                Id = vPotholeId, TenantId = vantaaId,
                Slug = "tievaurio-lomake",
                Title = "Ilmoita tievauriosta",
                Description = "Käytä tätä lomaketta ilmoittaaksesi tievauriosta Vantaalla.",
                AllowAnonymous = true, RequireSuomiFi = false,
                Fields = PotholeFields(vPotholeId)
            },
            new FormDefinition
            {
                Id = vTreeId, TenantId = vantaaId,
                Slug = "puunkaato-lomake",
                Title = "Pyydä puunkaatoa",
                Description = "Pyydä kaupungin omistaman puun kaatoa tai leikkausta tontillasi.",
                AllowAnonymous = true, RequireSuomiFi = false,
                Fields = TreeTrimmingFields(vTreeId)
            },
            new FormDefinition
            {
                Id = vNoiseId, TenantId = vantaaId,
                Slug = "meluhaitta-lomake",
                Title = "Meluilmoitus",
                Description = "Tee ilmoitus jatkuvasta meluhaitasta. Edellyttää tunnistautumista.",
                AllowAnonymous = false, RequireSuomiFi = true,
                Fields = NoiseComplaintFields(vNoiseId, includeRepeatOffenceField: true)
            }
        );

        // ── Vantaa demo submissions ──────────────────────────────────────────
        db.FormSubmissions.AddRange(
            new FormSubmission
            {
                Id = Guid.NewGuid(), TenantId = vantaaId,
                FormDefinitionId = vPotholeId,
                FormSlug = "tievaurio-lomake", FormTitle = "Ilmoita tievauriosta",
                SubmittedAt = DateTime.UtcNow.AddDays(-5),
                IsAnonymous = true, AnonymousEmail = "paavo@example.fi",
                Status = SubmissionStatus.InProgress, AssignedTo = "Tiekunnossapitotiimi",
                FormData = new()
                {
                    ["reporterName"]      = "Paavo Virtanen",
                    ["reporterEmail"]     = "paavo@example.fi",
                    ["streetAddress"]     = "Tikkurilantie 22",
                    ["severity"]          = "Vaarallinen — välitön riski",
                    ["dangerDescription"] = "Suuri kuoppa, noin 40 cm leveä, bussipysäkin vieressä.",
                    ["description"]       = "Kuoppa ollut useita viikkoja. Aiheuttaa vaaratilanteita"
                }
            },
            new FormSubmission
            {
                Id = Guid.NewGuid(), TenantId = vantaaId,
                FormDefinitionId = vNoiseId,
                FormSlug = "meluhaitta-lomake", FormTitle = "Meluilmoitus",
                SubmittedAt = DateTime.UtcNow.AddDays(-2),
                IsAnonymous = false, SuomiFiName = "Liisa Korhonen", SuomiFiHetu = "010180-1234",
                Status = SubmissionStatus.New,
                FormData = new()
                {
                    ["complainantName"]    = "Liisa Korhonen",
                    ["complainantAddress"] = "Ratatie 8 A 4",
                    ["noiseAddress"]       = "Ratatie 8 B 12",
                    ["noiseType"]          = "Kova musiikki",
                    ["timeOfDay"]          = "Yöaika (22–07)",
                    ["isRepeatOffence"]    = "true",
                    ["description"]        = "Kovaa musiikkia soitetaan joka perjantai- ja lauantai-ilta, nukkuminen on mahdotonta."
                }
            },
            new FormSubmission
            {
                Id = Guid.NewGuid(), TenantId = vantaaId,
                FormDefinitionId = vTreeId,
                FormSlug = "puunkaato-lomake", FormTitle = "Pyydä puunkaatoa",
                SubmittedAt = DateTime.UtcNow.AddDays(-10),
                IsAnonymous = true, Status = SubmissionStatus.Resolved,
                FormData = new()
                {
                    ["requestorName"]   = "Mikko Leinonen",
                    ["requestorEmail"]  = "mikko@example.fi",
                    ["treeAddress"]     = "Puistokatu 3",
                    ["treeDescription"] = "Vanha koivu, iso oksa roikkuu jalkakäytävän yläpuolella ja naarmuttaa autoja. Vaara katkeamisesta",
                    ["urgency"]         = "Kohtalainen — kuukauden sisällä"
                }
            }
        );

        // ── Espoo forms ──────────────────────────────────────────────────────
        var ePotholeId = Guid.NewGuid();
        var eNoiseId   = Guid.NewGuid();

        var espooPotholeFields = PotholeFields(ePotholeId);
        espooPotholeFields.AddRange(EspooExtraPotholeFields(ePotholeId));

        db.FormDefinitions.AddRange(
            new FormDefinition
            {
                Id = ePotholeId, TenantId = espooId,
                Slug = "tievaurio-lomake",
                Title = "Ilmoita tievauriosta",
                Description = "Ilmoita kuopasta tai tievauriosta Espoossa. Espoon kaupunki kerää yksityiskohtaisia tietoja kunnossapidon tehostamiseksi.",
                AllowAnonymous = true, RequireSuomiFi = false,
                Fields = espooPotholeFields
            },
            new FormDefinition
            {
                Id = eNoiseId, TenantId = espooId,
                Slug = "meluhaitta-lomake",
                Title = "Meluilmoitus",
                Description = "Tee ilmoitus meluhaitasta. Edellyttää tunnistautumista.",
                AllowAnonymous = false, RequireSuomiFi = true,
                Fields = NoiseComplaintFields(eNoiseId, includeRepeatOffenceField: false)
            }
        );

        await db.SaveChangesAsync();
    }

    // ─── Shared field factories (unchanged from InMemoryTenantStore) ─────────

    private static List<FormField> PotholeFields(Guid formId) =>
    [
        new() { Id=Guid.NewGuid(), FormDefinitionId=formId,
                FieldKey="reporterName",    Label="Nimesi",
                FieldType=FieldTypes.Text,  IsRequired=false, DisplayOrder=10,
                GroupName="Yhteystietosi",  Placeholder="Valinnainen" },

        new() { Id=Guid.NewGuid(), FormDefinitionId=formId,
                FieldKey="reporterEmail",   Label="Sähköpostiosoite",
                FieldType=FieldTypes.Email, IsRequired=false, DisplayOrder=20,
                GroupName="Yhteystietosi",  Placeholder="Valinnainen — tilapäivityksiä varten",
                HelpText="Käytämme tätä vain ilmoituksesi tilapäivityksiin." },

        new() { Id=Guid.NewGuid(), FormDefinitionId=formId,
                FieldKey="streetAddress",   Label="Vaurion katuosoite",
                FieldType=FieldTypes.Text,  IsRequired=true,  DisplayOrder=30,
                GroupName="Sijainti",       Placeholder="esim. Keskuskatu 10" },

        new() { Id=Guid.NewGuid(), FormDefinitionId=formId,
                FieldKey="severity",        Label="Kuinka vakava vaurio on?",
                FieldType=FieldTypes.Radio, IsRequired=true,  DisplayOrder=40,
                GroupName="Vaurion tiedot",
                Options="""["Lievä — vain pintavaurio","Kohtalainen — haittaa ajoa","Vaarallinen — välitön riski"]""" },

        new() { Id=Guid.NewGuid(), FormDefinitionId=formId,
                FieldKey="dangerDescription",  Label="Kuvaile välitön vaara",
                FieldType=FieldTypes.Textarea, IsRequired=false, DisplayOrder=50,
                GroupName="Vaurion tiedot",
                Placeholder="esim. Suuri kuoppa raitiotiekiskojen vieressä...",
                ConditionalOnField="severity",
                ConditionalOnValue="Vaarallinen — välitön riski",
                HelpText="Tämä ohjataan hätäkunnossapitotiimille." },

        new() { Id=Guid.NewGuid(), FormDefinitionId=formId,
                FieldKey="description",       Label="Lisätiedot",
                FieldType=FieldTypes.Textarea, IsRequired=false, DisplayOrder=60,
                GroupName="Vaurion tiedot",
                Placeholder="Muita huomioita..." },

        new() { Id=Guid.NewGuid(), FormDefinitionId=formId,
                FieldKey="photo",          Label="Liitä kuva",
                FieldType=FieldTypes.File, IsRequired=false, DisplayOrder=70,
                GroupName="Vaurion tiedot",
                HelpText="JPG tai PNG, enintään 5 Mt." },
    ];

    private static List<FormField> EspooExtraPotholeFields(Guid formId) =>
    [
        new() { Id=Guid.NewGuid(), FormDefinitionId=formId,
                FieldKey="district",        Label="Kaupunginosa",
                FieldType=FieldTypes.Select, IsRequired=true, DisplayOrder=25,
                GroupName="Sijainti",
                Options="""["Tapiola","Leppävaara","Espoon keskus","Matinkylä","Espoonlahti","Keilaniemi","Otaniemi","Kivenlahti"]""" },

        new() { Id=Guid.NewGuid(), FormDefinitionId=formId,
                FieldKey="nearestLandmark", Label="Lähin maamerkin kuvaus",
                FieldType=FieldTypes.Text,  IsRequired=false, DisplayOrder=26,
                GroupName="Sijainti",
                Placeholder="esim. Metroaseman sisäänkäynnin vieressä",
                HelpText="Auttaa kunnossapitotiimiä löytämään vaurion nopeammin." },

        new() { Id=Guid.NewGuid(), FormDefinitionId=formId,
                FieldKey="roadType",         Label="Tien tyyppi",
                FieldType=FieldTypes.Select, IsRequired=true, DisplayOrder=27,
                GroupName="Sijainti",
                Options="""["Pääkatu","Sivukatu","Pyörätie","Jalkakäytävä","Parkkialue"]""" },

        new() { Id=Guid.NewGuid(), FormDefinitionId=formId,
                FieldKey="estimatedSize",    Label="Arvioitu koko",
                FieldType=FieldTypes.Select, IsRequired=true, DisplayOrder=42,
                GroupName="Vaurion tiedot",
                Options="""["Pieni (alle 20 cm)","Keskikokoinen (20–50 cm)","Suuri (yli 50 cm)"]""" },

        new() { Id=Guid.NewGuid(), FormDefinitionId=formId,
                FieldKey="estimatedDepth",   Label="Arvioitu syvyys",
                FieldType=FieldTypes.Select, IsRequired=false, DisplayOrder=43,
                GroupName="Vaurion tiedot",
                Options="""["Matala (alle 5 cm)","Kohtalainen (5–10 cm)","Syvä (yli 10 cm)"]""" },

        new() { Id=Guid.NewGuid(), FormDefinitionId=formId,
                FieldKey="trafficImpact",    Label="Vaikutus liikenteeseen",
                FieldType=FieldTypes.Radio,  IsRequired=true, DisplayOrder=44,
                GroupName="Vaurion tiedot",
                Options="""["Ei vaikutusta","Hidastaa liikennettä","Estää kaistan käytön","Sulkee tien kokonaan"]""" },

        new() { Id=Guid.NewGuid(), FormDefinitionId=formId,
                FieldKey="firstObserved",    Label="Milloin havaitsit vaurion ensimmäisen kerran?",
                FieldType=FieldTypes.Date,   IsRequired=false, DisplayOrder=45,
                GroupName="Vaurion tiedot" },

        new() { Id=Guid.NewGuid(), FormDefinitionId=formId,
                FieldKey="previouslyReported", Label="Olen ilmoittanut tästä vauriosta aiemmin",
                FieldType=FieldTypes.Checkbox, IsRequired=false, DisplayOrder=46,
                GroupName="Vaurion tiedot",
                HelpText="Merkitse tämä, jos olet tehnyt aiemmasta ilmoituksesta." },

        new() { Id=Guid.NewGuid(), FormDefinitionId=formId,
                FieldKey="reporterRole",     Label="Ilmoittajan rooli",
                FieldType=FieldTypes.Select, IsRequired=true, DisplayOrder=15,
                GroupName="Yhteystietosi",
                Options="""["Asukas","Isännöitsijä","Yritys","Muu"]""" },

        new() { Id=Guid.NewGuid(), FormDefinitionId=formId,
                FieldKey="reporterPhone",    Label="Puhelinnumero",
                FieldType=FieldTypes.Phone,  IsRequired=false, DisplayOrder=22,
                GroupName="Yhteystietosi",
                Placeholder="Valinnainen" },

        new() { Id=Guid.NewGuid(), FormDefinitionId=formId,
                FieldKey="contactPreference", Label="Yhteydenottotapa",
                FieldType=FieldTypes.Select,  IsRequired=false, DisplayOrder=23,
                GroupName="Yhteystietosi",
                Options="""["Sähköposti","Puhelin","Ei yhteydenottoa"]""",
                HelpText="Kuinka haluaisit saada vastauksen pyyntöösi?" },
    ];

    private static List<FormField> TreeTrimmingFields(Guid formId) =>
    [
        new() { Id=Guid.NewGuid(), FormDefinitionId=formId,
                FieldKey="requestorName",   Label="Nimesi",
                FieldType=FieldTypes.Text,  IsRequired=true,  DisplayOrder=10,
                GroupName="Yhteystietosi" },

        new() { Id=Guid.NewGuid(), FormDefinitionId=formId,
                FieldKey="requestorEmail",  Label="Sähköpostiosoite",
                FieldType=FieldTypes.Email, IsRequired=true,  DisplayOrder=20,
                GroupName="Yhteystietosi" },

        new() { Id=Guid.NewGuid(), FormDefinitionId=formId,
                FieldKey="requestorPhone",  Label="Puhelinnumero",
                FieldType=FieldTypes.Phone, IsRequired=false, DisplayOrder=30,
                GroupName="Yhteystietosi" },

        new() { Id=Guid.NewGuid(), FormDefinitionId=formId,
                FieldKey="treeAddress",     Label="Puun sijainti",
                FieldType=FieldTypes.Text,  IsRequired=true,  DisplayOrder=40,
                GroupName="Puun tiedot",    Placeholder="esim. Ratatie 5" },

        new() { Id=Guid.NewGuid(), FormDefinitionId=formId,
                FieldKey="treeDescription", Label="Kuvaile puuta ja ongelmaa",
                FieldType=FieldTypes.Textarea, IsRequired=true, DisplayOrder=50,
                GroupName="Puun tiedot",
                Placeholder="esim. Iso koivu, oksat roikkuvat tien yläpuolella..." },

        new() { Id=Guid.NewGuid(), FormDefinitionId=formId,
                FieldKey="urgency",          Label="Kiireellisyys",
                FieldType=FieldTypes.Select, IsRequired=true, DisplayOrder=60,
                GroupName="Puun tiedot",
                Options="""["Matala — seuraava aikataulutettu kierros","Kohtalainen — kuukauden sisällä","Korkea — turvallisuusriski"]""" },
    ];

    private static List<FormField> NoiseComplaintFields(Guid formId, bool includeRepeatOffenceField)
    {
        var fields = new List<FormField>
        {
            new() { Id=Guid.NewGuid(), FormDefinitionId=formId,
                    FieldKey="suomifiInfo",    Label="",
                    FieldType=FieldTypes.Info, IsRequired=false, DisplayOrder=5,
                    HelpText="<strong>Henkilöllisyytesi on vahvistettu Suomi.fi-palvelun kautta.</strong> Nimesi ja osoitteesi on esitäytetty väestötietojärjestelmästä." },

            new() { Id=Guid.NewGuid(), FormDefinitionId=formId,
                    FieldKey="complainantName",  Label="Nimesi",
                    FieldType=FieldTypes.Text,   IsRequired=true, DisplayOrder=10,
                    GroupName="Yhteystietosi" },

            new() { Id=Guid.NewGuid(), FormDefinitionId=formId,
                    FieldKey="complainantAddress", Label="Osoitteesi",
                    FieldType=FieldTypes.Text,     IsRequired=true, DisplayOrder=20,
                    GroupName="Yhteystietosi" },

            new() { Id=Guid.NewGuid(), FormDefinitionId=formId,
                    FieldKey="noiseAddress",    Label="Osoite, jossa meluhaittaa",
                    FieldType=FieldTypes.Text,  IsRequired=true, DisplayOrder=30,
                    GroupName="Ilmoituksen tiedot" },

            new() { Id=Guid.NewGuid(), FormDefinitionId=formId,
                    FieldKey="noiseType",        Label="Melun tyyppi",
                    FieldType=FieldTypes.Select, IsRequired=true, DisplayOrder=40,
                    GroupName="Ilmoituksen tiedot",
                    Options="""["Kova musiikki","Rakentaminen työajan ulkopuolella","Muu melu naapurissa hiljaisuuden aikana","Teollisuusmelu","Muu"]""" },

            new() { Id=Guid.NewGuid(), FormDefinitionId=formId,
                    FieldKey="noiseTypeOther",  Label="Kuvaile melun tyyppi",
                    FieldType=FieldTypes.Text,  IsRequired=false, DisplayOrder=45,
                    GroupName="Ilmoituksen tiedot",
                    ConditionalOnField="noiseType", ConditionalOnValue="Muu" },

            new() { Id=Guid.NewGuid(), FormDefinitionId=formId,
                    FieldKey="timeOfDay",        Label="Milloin melu esiintyy?",
                    FieldType=FieldTypes.Select, IsRequired=true, DisplayOrder=50,
                    GroupName="Ilmoituksen tiedot",
                    Options="""["Päiväaika (07–22)","Yöaika (22–07)","Molemmat"]""" },

            new() { Id=Guid.NewGuid(), FormDefinitionId=formId,
                    FieldKey="description",        Label="Kuvaus",
                    FieldType=FieldTypes.Textarea, IsRequired=true, DisplayOrder=60,
                    GroupName="Ilmoituksen tiedot",
                    Placeholder="Kuvaile häiriötä yksityiskohtaisesti..." },
        };

        if (includeRepeatOffenceField)
        {
            fields.Add(new FormField
            {
                Id = Guid.NewGuid(), FormDefinitionId = formId,
                FieldKey = "isRepeatOffence",
                Label = "Kyseessä on toistuva häiriö",
                FieldType = FieldTypes.Checkbox,
                IsRequired = false, DisplayOrder = 65,
                GroupName = "Ilmoituksen tiedot",
                HelpText = "Merkitse tämä, jos olet tehnyt ilmoituksen samasta osoitteesta aiemmin."
            });
        }

        return fields;
    }
}
