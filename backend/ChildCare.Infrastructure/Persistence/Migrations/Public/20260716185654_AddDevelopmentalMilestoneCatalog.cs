using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChildCare.Infrastructure.Persistence.Migrations.Public
{
    /// <inheritdoc />
    public partial class AddDevelopmentalMilestoneCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "developmental_domains",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    NameNl = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameFr = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_developmental_domains", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "developmental_milestones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DomainId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgeFromMonths = table.Column<int>(type: "integer", nullable: false),
                    AgeToMonths = table.Column<int>(type: "integer", nullable: false),
                    DescriptionNl = table.Column<string>(type: "text", nullable: false),
                    DescriptionFr = table.Column<string>(type: "text", nullable: false),
                    DescriptionEn = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_developmental_milestones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_developmental_milestones_developmental_domains_DomainId",
                        column: x => x.DomainId,
                        principalTable: "developmental_domains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_developmental_domains_Code",
                table: "developmental_domains",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_developmental_milestones_AgeFromMonths_AgeToMonths",
                table: "developmental_milestones",
                columns: new[] { "AgeFromMonths", "AgeToMonths" });

            migrationBuilder.CreateIndex(
                name: "IX_developmental_milestones_DomainId_SortOrder",
                table: "developmental_milestones",
                columns: new[] { "DomainId", "SortOrder" });

            // Seed data (data-model.md, research.md R7) — the 7 domains named in BACKLOG.md plus
            // milestones spanning the 0-36 month daycare age range, drawn from standard
            // Flemish/Belgian early-childhood developmental checkpoints. Content, not a scope
            // decision (research.md R7) — reviewed/applied manually like any other migration
            // (constitution Principle VI), not auto-applied in production. Age bands are
            // non-overlapping and inclusive on both ends.
            var seededAt = new DateTime(2026, 7, 16, 0, 0, 0, DateTimeKind.Utc);

            var domains = new (string Code, string Nl, string Fr, string En, int Sort, Guid Id)[]
            {
                ("motor_gross", "Grove motoriek",      "Motricité globale", "Gross motor", 1, Guid.Parse("d1000000-0000-4a00-8000-000000000000")),
                ("motor_fine",  "Fijne motoriek",       "Motricité fine",    "Fine motor",  2, Guid.Parse("d2000000-0000-4a00-8000-000000000000")),
                ("language",    "Taal",                 "Langage",           "Language",    3, Guid.Parse("d3000000-0000-4a00-8000-000000000000")),
                ("cognitive",   "Cognitie",             "Cognition",         "Cognitive",   4, Guid.Parse("d4000000-0000-4a00-8000-000000000000")),
                ("social",      "Sociaal",              "Social",            "Social",      5, Guid.Parse("d5000000-0000-4a00-8000-000000000000")),
                ("emotional",   "Emotioneel",           "Émotionnel",        "Emotional",   6, Guid.Parse("d6000000-0000-4a00-8000-000000000000")),
                ("self_care",   "Zelfredzaamheid",      "Autonomie",         "Self-care",   7, Guid.Parse("d7000000-0000-4a00-8000-000000000000")),
            };

            migrationBuilder.InsertData(
                table: "developmental_domains",
                columns: new[] { "Id", "Code", "NameNl", "NameFr", "NameEn", "SortOrder" },
                values: domains.Select(d => new object[] { d.Id, d.Code, d.Nl, d.Fr, d.En, d.Sort }).ToList().ToArray2D(6));

            // (AgeFrom, AgeTo, Nl, Fr, En) per domain, in age-band order — non-overlapping,
            // inclusive both ends.
            var milestonesByDomain = new Dictionary<Guid, (int From, int To, string Nl, string Fr, string En)[]>
            {
                [domains[0].Id] = new (int, int, string, string, string)[]
                {
                    (0, 3,   "Til het hoofdje even op in buikligging", "Soulève brièvement la tête en position ventrale", "Briefly lifts head while lying on tummy"),
                    (4, 6,   "Rolt van buik naar rug (en omgekeerd)", "Se retourne du ventre vers le dos (et inversement)", "Rolls from tummy to back (and back again)"),
                    (7, 12,  "Kruipt zelfstandig vooruit", "Rampe de façon autonome", "Crawls independently"),
                    (13, 18, "Loopt zelfstandig enkele stappen", "Marche seul sur quelques pas", "Walks independently for a few steps"),
                    (19, 24, "Klimt op en van laag meubilair", "Monte et descend d'un meuble bas", "Climbs onto and off low furniture"),
                    (25, 36, "Springt met beide voeten tegelijk van de grond", "Saute à pieds joints", "Jumps with both feet off the ground"),
                },
                [domains[1].Id] = new (int, int, string, string, string)[]
                {
                    (0, 3,   "Opent en sluit de handjes bewust", "Ouvre et ferme les mains consciemment", "Opens and closes hands purposefully"),
                    (4, 6,   "Grijpt bewust naar een voorwerp", "Attrape un objet de façon intentionnelle", "Reaches for and grasps an object intentionally"),
                    (7, 12,  "Gebruikt duim en wijsvinger (pincetgreep) om kleine dingen op te pakken", "Utilise le pouce et l'index (prise en pince) pour ramasser de petits objets", "Uses thumb and forefinger (pincer grasp) to pick up small items"),
                    (13, 18, "Bouwt een torentje van 2 blokjes", "Construit une tour de 2 cubes", "Builds a tower of 2 blocks"),
                    (19, 24, "Bladert zelfstandig (dikke) bladzijden om in een boek", "Tourne seul les pages (épaisses) d'un livre", "Turns (thick) pages of a book independently"),
                    (25, 36, "Tekent een verticale en horizontale lijn na", "Reproduit une ligne verticale et horizontale", "Copies a vertical and horizontal line"),
                },
                [domains[2].Id] = new (int, int, string, string, string)[]
                {
                    (0, 3,   "Reageert op geluid door te stoppen of om te draaien", "Réagit à un son en s'arrêtant ou en se tournant", "Reacts to sound by pausing or turning"),
                    (4, 6,   "Brabbelt met verschillende klanken", "Babille avec différents sons", "Babbles using different sounds"),
                    (7, 12,  "Zegt de eerste woordjes met betekenis (bv. 'mama', 'papa')", "Dit ses premiers mots avec du sens (p.ex. « maman », « papa »)", "Says first meaningful words (e.g. 'mama', 'dada')"),
                    (13, 18, "Begrijpt en volgt een eenvoudige opdracht ('geef de bal')", "Comprend et suit une consigne simple (« donne le ballon »)", "Understands and follows a simple instruction ('give the ball')"),
                    (19, 24, "Combineert twee woorden tot een zinnetje ('mama weg')", "Combine deux mots en une petite phrase (« maman partie »)", "Combines two words into a short phrase ('mommy gone')"),
                    (25, 36, "Vertelt in korte zinnen van 3-4 woorden", "Raconte en phrases courtes de 3 à 4 mots", "Talks in short 3-4 word sentences"),
                },
                [domains[3].Id] = new (int, int, string, string, string)[]
                {
                    (0, 3,   "Volgt een bewegend voorwerp met de ogen", "Suit un objet en mouvement des yeux", "Follows a moving object with the eyes"),
                    (4, 6,   "Zoekt naar de bron van een geluid", "Cherche la source d'un son", "Searches for the source of a sound"),
                    (7, 12,  "Begrijpt objectpermanentie (zoekt een verstopt voorwerp)", "Comprend la permanence de l'objet (cherche un objet caché)", "Understands object permanence (looks for a hidden object)"),
                    (13, 18, "Gebruikt een voorwerp op de bedoelde manier (bv. drinkt uit bekertje)", "Utilise un objet de la manière prévue (p.ex. boit dans un gobelet)", "Uses an object the way it's intended (e.g. drinks from a cup)"),
                    (19, 24, "Doet eenvoudig doen-alsof-spel na (bv. poppetje eten geven)", "Imite un jeu de faire-semblant simple (p.ex. donner à manger à une poupée)", "Imitates simple pretend play (e.g. feeding a doll)"),
                    (25, 36, "Sorteert voorwerpen op één kenmerk (kleur of vorm)", "Trie des objets selon une caractéristique (couleur ou forme)", "Sorts objects by one feature (colour or shape)"),
                },
                [domains[4].Id] = new (int, int, string, string, string)[]
                {
                    (0, 3,   "Kijkt naar en volgt gezichten", "Regarde et suit les visages", "Looks at and tracks faces"),
                    (4, 6,   "Lacht spontaan naar een bekend gezicht", "Sourit spontanément à un visage familier", "Smiles spontaneously at a familiar face"),
                    (7, 12,  "Speelt kiekeboe mee", "Participe au jeu de coucou", "Participates in peekaboo"),
                    (13, 18, "Speelt naast andere kinderen (parallel spel)", "Joue à côté d'autres enfants (jeu parallèle)", "Plays alongside other children (parallel play)"),
                    (19, 24, "Toont interesse in wat een ander kind doet", "Montre de l'intérêt pour ce que fait un autre enfant", "Shows interest in what another child is doing"),
                    (25, 36, "Speelt kort samen met een ander kind in een gedeeld spel", "Joue brièvement avec un autre enfant dans un jeu partagé", "Briefly plays together with another child in shared play"),
                },
                [domains[5].Id] = new (int, int, string, string, string)[]
                {
                    (0, 3,   "Kalmeert bij troost van een vertrouwd persoon", "Se calme lorsqu'il est réconforté par une personne familière", "Calms down when comforted by a familiar person"),
                    (4, 6,   "Toont duidelijk plezier (lachen, kirren)", "Montre clairement du plaisir (rire, gazouiller)", "Clearly shows enjoyment (laughing, cooing)"),
                    (7, 12,  "Vertoont vreemdenangst en hechting aan vertrouwde volwassene", "Manifeste une peur de l'étranger et un attachement à un adulte familier", "Shows stranger anxiety and attachment to a familiar adult"),
                    (13, 18, "Zoekt actief nabijheid van een vertrouwde volwassene bij onrust", "Recherche activement la proximité d'un adulte familier en cas de détresse", "Actively seeks closeness to a familiar adult when distressed"),
                    (19, 24, "Toont een breder gamma aan emoties (frustratie, trots, jaloezie)", "Montre une palette plus large d'émotions (frustration, fierté, jalousie)", "Shows a wider range of emotions (frustration, pride, jealousy)"),
                    (25, 36, "Benoemt een eigen emotie met een woord ('boos', 'blij')", "Nomme sa propre émotion avec un mot (« fâché », « content »)", "Names their own emotion with a word ('angry', 'happy')"),
                },
                [domains[6].Id] = new (int, int, string, string, string)[]
                {
                    (0, 3,   "Toont hongersignalen vóór het huilen (bv. zuigbewegingen)", "Montre des signaux de faim avant de pleurer (p.ex. mouvements de succion)", "Shows hunger cues before crying (e.g. sucking motions)"),
                    (4, 6,   "Houdt zelf de zuigfles of borst vast tijdens het drinken", "Tient lui-même le biberon ou le sein pendant qu'il boit", "Holds the bottle or breast themselves while feeding"),
                    (7, 12,  "Eet zelfstandig vingervoedsel", "Mange seul des aliments à manger avec les doigts", "Feeds themselves finger foods independently"),
                    (13, 18, "Probeert zelf met lepel te eten", "Essaie de manger seul à la cuillère", "Tries to eat with a spoon independently"),
                    (19, 24, "Helpt actief mee bij het aan- en uitkleden (bv. arm door mouw steken)", "Aide activement à s'habiller et se déshabiller (p.ex. passer le bras dans la manche)", "Actively helps with dressing/undressing (e.g. pushing an arm through a sleeve)"),
                    (25, 36, "Wast en droogt zelfstandig de handen", "Se lave et s'essuie les mains de façon autonome", "Washes and dries their own hands independently"),
                },
            };

            var milestoneRows = new List<object[]>();
            foreach (var domain in domains)
            {
                var milestones = milestonesByDomain[domain.Id];
                for (var i = 0; i < milestones.Length; i++)
                {
                    var m = milestones[i];
                    // e{domainSort}00000{index}-0000-4a00-8000-000000000000 — domainSort is 1-7,
                    // index is 1-6, both valid hex digits, so this always yields a well-formed,
                    // deterministic, collision-free GUID per (domain, milestone) pair.
                    milestoneRows.Add(new object[]
                    {
                        Guid.Parse($"e{domain.Sort}00000{i + 1}-0000-4a00-8000-000000000000"),
                        domain.Id,
                        m.From,
                        m.To,
                        m.Nl,
                        m.Fr,
                        m.En,
                        i + 1,
                    });
                }
            }

            migrationBuilder.InsertData(
                table: "developmental_milestones",
                columns: new[] { "Id", "DomainId", "AgeFromMonths", "AgeToMonths", "DescriptionNl", "DescriptionFr", "DescriptionEn", "SortOrder" },
                values: milestoneRows.ToArray2D(8));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "developmental_milestones");

            migrationBuilder.DropTable(
                name: "developmental_domains");
        }
    }

    internal static class MigrationArrayExtensions
    {
        public static object[,] ToArray2D(this IReadOnlyList<object[]> rows, int columnCount)
        {
            var result = new object[rows.Count, columnCount];
            for (var r = 0; r < rows.Count; r++)
                for (var c = 0; c < columnCount; c++)
                    result[r, c] = rows[r][c];
            return result;
        }
    }
}
