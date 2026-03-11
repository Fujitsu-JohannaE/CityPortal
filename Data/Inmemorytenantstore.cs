using CityPortal.Models;

namespace CityPortal.Data;

/// <summary>
/// Simulates two separate tenant databases in memory.
/// In production each tenant has its own SQL Server DB + EF Core DbContext.
/// </summary>
public class InMemoryTenantStore
{
    // Two "databases" — one per tenant
    private readonly Dictionary<Guid, TenantDb> _databases = new();

    public InMemoryTenantStore()
    {
        SeedVantaa();
        SeedEspoo();
    }

    public TenantDb GetDb(Guid tenantId) =>
        _databases.TryGetValue(tenantId, out var db)
            ? db
            : throw new KeyNotFoundException($"Tenant {tenantId} not found");

    public List<Tenant> GetAllTenants() =>
        _databases.Values.Select(d => d.Tenant).ToList();

    public Tenant? FindTenantBySlug(string slug) =>
        _databases.Values.FirstOrDefault(d => d.Tenant.Slug == slug)?.Tenant;

    // ─── Vantaa seed ─────────────────────────────────────────────────────────

    private void SeedVantaa()
    {
        var tenantId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
        var db = new TenantDb
        {
            Tenant = new Tenant { Id = tenantId, Name = "Vantaan kaupunki", Slug = "vantaa" }
        };

        // Form 1: Pothole report (anonymous, has conditional field + file upload)
        var potholeId = Guid.NewGuid();
        db.FormDefinitions.Add(new FormDefinition
        {
            Id = potholeId,
            TenantId = tenantId,
            Slug = "pothole-report",
            Title = "Ilmoita tievauriosta",
            Description = "Käytä tätä lomaketta ilmoittaaksesi tievauriosta Vantaalla.",
            AllowAnonymous = true,
            RequireSuomiFi = false,
            Fields = PotholeFields(potholeId)
        });

        // Form 2: Tree trimming request (anonymous, simple)
        var treeId = Guid.NewGuid();
        db.FormDefinitions.Add(new FormDefinition
        {
            Id = treeId,
            TenantId = tenantId,
            Slug = "tree-trimming",
            Title = "Pyydä puunkaatoa",
            Description = "Pyydä kaupungin omistaman puun kaatoa tai leikkausta tontillasi.",
            AllowAnonymous = true,
            RequireSuomiFi = false,
            Fields = TreeTrimmingFields(treeId)
        });

        // Form 3: Noise complaint (Suomi.fi required) — Vantaa has an extra "repeat offence" field
        var noiseId = Guid.NewGuid();
        db.FormDefinitions.Add(new FormDefinition
        {
            Id = noiseId,
            TenantId = tenantId,
            Slug = "noise-complaint",
            Title = "Meluilmoitus",
            Description = "Tee ilmoitus jatkuvasta meluhaitasta. Edellyttää tunnistautumista.",
            AllowAnonymous = false,
            RequireSuomiFi = true,
            Fields = NoiseComplaintFields(noiseId, includeRepeatOffenceField: true)
        });

        SeedDemoSubmissions(db, tenantId);
        _databases[tenantId] = db;
    }

    // ─── Espoo seed ──────────────────────────────────────────────────────────

    private void SeedEspoo()
    {
        var tenantId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
        var db = new TenantDb
        {
            Tenant = new Tenant { Id = tenantId, Name = "Espoon kaupunki", Slug = "espoo" }
        };
        // Espoo pothole form — starts from the shared base then adds MANY extra fields
        // to clearly demonstrate per-tenant field variation
        var potholeId = Guid.NewGuid();
        var potholeFields = PotholeFields(potholeId);
        potholeFields.AddRange(new[]
        {
            // Extra location fields
            new FormField {
                Id=Guid.NewGuid(), FormDefinitionId=potholeId,
                FieldKey="district",        Label="Kaupunginosa",
                FieldType=FieldTypes.Select, IsRequired=true, DisplayOrder=25,
                GroupName="Sijainti",
                Options="""["Tapiola","Leppävaara","Espoon keskus","Matinkylä","Espoonlahti","Keilaniemi","Otaniemi","Kivenlahti"]""" },

            new FormField {
                Id=Guid.NewGuid(), FormDefinitionId=potholeId,
                FieldKey="nearestLandmark", Label="Lähin maamerkin kuvaus",
                FieldType=FieldTypes.Text,  IsRequired=false, DisplayOrder=26,
                GroupName="Sijainti",
                Placeholder="esim. Metroaseman sisäänkäynnin vieressä",
                HelpText="Auttaa kunnossapitotiimiä löytämään vaurion nopeammin." },

            new FormField {
                Id=Guid.NewGuid(), FormDefinitionId=potholeId,
                FieldKey="roadType",         Label="Tien tyyppi",
                FieldType=FieldTypes.Select, IsRequired=true, DisplayOrder=27,
                GroupName="Sijainti",
                Options="""["Pääkatu","Sivukatu","Pyörätie","Jalkakäytävä","Parkkialue"]""" },

            // Extra incident detail fields
            new FormField {
                Id=Guid.NewGuid(), FormDefinitionId=potholeId,
                FieldKey="estimatedSize",    Label="Arvioitu koko",
                FieldType=FieldTypes.Select, IsRequired=true, DisplayOrder=42,
                GroupName="Vaurion tiedot",
                Options="""["Pieni (alle 20 cm)","Keskikokoinen (20–50 cm)","Suuri (yli 50 cm)"]""" },

            new FormField {
                Id=Guid.NewGuid(), FormDefinitionId=potholeId,
                FieldKey="estimatedDepth",   Label="Arvioitu syvyys",
                FieldType=FieldTypes.Select, IsRequired=false, DisplayOrder=43,
                GroupName="Vaurion tiedot",
                Options="""["Matala (alle 5 cm)","Kohtalainen (5–10 cm)","Syvä (yli 10 cm)"]""" },

            new FormField {
                Id=Guid.NewGuid(), FormDefinitionId=potholeId,
                FieldKey="trafficImpact",    Label="Vaikutus liikenteeseen",
                FieldType=FieldTypes.Radio,  IsRequired=true, DisplayOrder=44,
                GroupName="Vaurion tiedot",
                Options="""["Ei vaikutusta","Hidastaa liikennettä","Estää kaistan käytön","Sulkee tien kokonaan"]""" },

            new FormField {
                Id=Guid.NewGuid(), FormDefinitionId=potholeId,
                FieldKey="firstObserved",    Label="Milloin havaitsit vaurion ensimmäisen kerran?",
                FieldType=FieldTypes.Date,   IsRequired=false, DisplayOrder=45,
                GroupName="Vaurion tiedot" },

            new FormField {
                Id=Guid.NewGuid(), FormDefinitionId=potholeId,
                FieldKey="previouslyReported", Label="Olen ilmoittanut tästä vauriosta aiemmin",
                FieldType=FieldTypes.Checkbox, IsRequired=false, DisplayOrder=46,
                GroupName="Vaurion tiedot",
                HelpText="Merkitse tämä, jos olet tehnyt aiemmasta ilmoituksesta." },

            // Extra reporter details
            new FormField {
                Id=Guid.NewGuid(), FormDefinitionId=potholeId,
                FieldKey="reporterRole",     Label="Ilmoittajan rooli",
                FieldType=FieldTypes.Select, IsRequired=true, DisplayOrder=15,
                GroupName="Yhteystietosi",
                Options="""["Asukas","Isännöitsijä","Yritys","Muu"]""" },

            new FormField {
                Id=Guid.NewGuid(), FormDefinitionId=potholeId,
                FieldKey="reporterPhone",    Label="Puhelinnumero",
                FieldType=FieldTypes.Phone,  IsRequired=false, DisplayOrder=22,
                GroupName="Yhteystietosi",
                Placeholder="Valinnainen" },

            new FormField {
                Id=Guid.NewGuid(), FormDefinitionId=potholeId,
                FieldKey="contactPreference", Label="Yhteydenottotapa",
                FieldType=FieldTypes.Select,  IsRequired=false, DisplayOrder=23,
                GroupName="Yhteystietosi",
                Options="""["Sähköposti","Puhelin","Ei yhteydenottoa"]""",
                HelpText="Kuinka haluaisit saada vastauksen pyyntöösi?" },
        });

        db.FormDefinitions.Add(new FormDefinition
        {
            Id = potholeId,
            TenantId = tenantId,
            Slug = "pothole-report",
            Title = "Ilmoita tievauriosta",
            Description = "Ilmoita kuopasta tai tievauriosta Espoossa. Espoon kaupunki kerää yksityiskohtaisia tietoja kunnossapidon tehostamiseksi.",
            AllowAnonymous = true,
            RequireSuomiFi = false,
            Fields = potholeFields
        });


        // Espoo noise complaint WITHOUT the repeat offence field
        var noiseId = Guid.NewGuid();
        db.FormDefinitions.Add(new FormDefinition
        {
            Id = noiseId,
            TenantId = tenantId,
            Slug = "noise-complaint",
            Title = "Meluilmoitus",
            Description = "Tee ilmoitus meluhaitasta. Edellyttää tunnistautumista.",
            AllowAnonymous = false,
            RequireSuomiFi = true,
            Fields = NoiseComplaintFields(noiseId, includeRepeatOffenceField: false)
        });

        _databases[tenantId] = db;
    }

    // ─── Shared field factories ───────────────────────────────────────────────

    private static List<FormField> PotholeFields(Guid formId) => new()
    {
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

        // Conditional: only shown when severity = "Vaarallinen — välitön riski"
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
    };

    private static List<FormField> TreeTrimmingFields(Guid formId) => new()
    {
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
    };

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

        // Vantaa adds this field, Espoo does not — demonstrates per-tenant field variation
        if (includeRepeatOffenceField)
        {
            fields.Add(new FormField
            {
                Id = Guid.NewGuid(),
                FormDefinitionId = formId,
                FieldKey = "isRepeatOffence",
                Label = "Kyseessä on toistuva häiriö",
                FieldType = FieldTypes.Checkbox,
                IsRequired = false,
                DisplayOrder = 65,
                GroupName = "Ilmoituksen tiedot",
                HelpText = "Merkitse tämä, jos olet tehnyt ilmoituksen samasta osoitteesta aiemmin."
            });
        }

        return fields;
    }

    // ─── Demo submissions ─────────────────────────────────────────────────────

    private static void SeedDemoSubmissions(TenantDb db, Guid tenantId)
    {
        var potholeDef = db.FormDefinitions.First(f => f.Slug == "pothole-report");
        var noiseDef = db.FormDefinitions.First(f => f.Slug == "noise-complaint");
        var treeDef = db.FormDefinitions.First(f => f.Slug == "tree-trimming");

        db.Submissions.AddRange(new[]
        {
            new FormSubmission
            {
                Id=Guid.NewGuid(), TenantId=tenantId,
                FormDefinitionId=potholeDef.Id,
                FormSlug="pothole-report", FormTitle="Ilmoita tievauriosta",
                SubmittedAt=DateTime.UtcNow.AddDays(-5),
                IsAnonymous=true,
                AnonymousEmail="paavo@example.fi",
                Status=SubmissionStatus.InProgress,
                AssignedTo="Tiekunnossapitotiimi",
                FormData=new(){
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
                Id=Guid.NewGuid(), TenantId=tenantId,
                FormDefinitionId=noiseDef.Id,
                FormSlug="noise-complaint", FormTitle="Meluilmoitus",
                SubmittedAt=DateTime.UtcNow.AddDays(-2),
                IsAnonymous=false,
                SuomiFiName="Liisa Korhonen",
                SuomiFiHetu="010180-1234",
                Status=SubmissionStatus.New,
                FormData=new(){
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
                Id=Guid.NewGuid(), TenantId=tenantId,
                FormDefinitionId=treeDef.Id,
                FormSlug="tree-trimming", FormTitle="Pyydä puunkaatoa",
                SubmittedAt=DateTime.UtcNow.AddDays(-10),
                IsAnonymous=true,
                Status=SubmissionStatus.Resolved,
                FormData=new(){
                    ["requestorName"]   = "Mikko Leinonen",
                    ["requestorEmail"]  = "mikko@example.fi",
                    ["treeAddress"]     = "Puistokatu 3",
                    ["treeDescription"] = "Vanha koivu, iso oksa roikkuu jalkakäytävän yläpuolella ja naarmuttaa autoja. Vaara katkeamisesta",
                    ["urgency"]         = "Kohtalainen — kuukauden sisällä"
                }
            },
        });
    }
}

// ─── Per-tenant "database" ───────────────────────────────────────────────────

public class TenantDb
{
    public Tenant Tenant { get; set; } = default!;
    public List<FormDefinition> FormDefinitions { get; set; } = new();
    public List<FormSubmission> Submissions { get; set; } = new();
}