using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Requests;
using ClerkAssistant.Api.Infrastructure.Common;
using ClerkAssistant.Api.Infrastructure.Letters;
using ClerkAssistant.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClerkAssistant.Api.Infrastructure.Seeding;

public sealed class DemoDataSeeder(
    AppDbContext db,
    UserManager<AppUser> users,
    CaseNumberGenerator numbers,
    AppClock clock,
    IHostEnvironment environment,
    IOptions<SeedOptions> seedOptions,
    IOptions<OrganizationOptions> organizationOptions,
    ILogger<DemoDataSeeder> logger)
{
    private static readonly TimeZoneInfo Warsaw = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly (string UserName, string FullName, string Initials)[] DemoClerks =
    [
        ("akowalska", "Anna Kowalska", "AK"),
        ("pnowak", "Piotr Nowak", "PN"),
        ("mwisniewska", "Maria Wiśniewska", "MW"),
    ];

    private static readonly Dictionary<string, int> DepartmentReplyDays = new()
    {
        ["WGK"] = 3, ["WIM"] = 6, ["WE"] = 4, ["WZB"] = 2, ["WMG"] = 8, ["WOŚ"] = 5, ["BKB"] = 3, ["WAB"] = 7,
        ["BAS"] = 2, ["BP"] = 4, ["WZP"] = 5, ["WPL"] = 3, ["WGO"] = 4, ["WZK"] = 6, ["BCB"] = 3, ["WI"] = 2,
        ["BZS"] = 5, ["WW"] = 4,
    };

    private OrganizationOptions Organization => organizationOptions.Value;

    public async Task SeedAsync()
    {
        if (await db.Requests.AnyAsync())
        {
            return;
        }

        var clerks = await EnsureClerksAsync();
        var departments = await db.Departments.ToDictionaryAsync(d => d.Symbol);
        var today = clock.Today;
        var samples = (await LoadSampleCasesAsync()).Select(PlanSample).ToList();
        var fictional = CurrentCases().Select(c => PlanCurrent(c, today))
            .Concat(ArchivedCases().Select(c => PlanArchived(c, today)))
            .OrderBy(p => p.ReceivedOn)
            .ToList();
        NumberAfter(samples, fictional);

        foreach (var plan in samples.Concat(fictional))
        {
            var clerk = clerks.Count == 0 ? null : clerks[plan.Clerk % clerks.Count];
            AddRequest(plan, clerk, departments, today);
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Seeded {Samples} sample and {Fictional} fictional demo requests", samples.Count, fictional.Count);
    }

    private async Task<List<AppUser>> EnsureClerksAsync()
    {
        var password = seedOptions.Value.DemoPassword;
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Seed:DemoPassword is not set; demo clerk accounts are not created and demo requests are assigned to existing users.");
        }

        var clerks = new List<AppUser>();
        foreach (var (userName, fullName, initials) in DemoClerks)
        {
            var user = await users.FindByNameAsync(userName);
            if (user is null && !string.IsNullOrWhiteSpace(password))
            {
                user = new AppUser { UserName = userName, FullName = fullName, Initials = initials, CreatedAt = clock.Now };
                var result = await users.CreateAsync(user, password);
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Could not create demo user {userName}: " + string.Join("; ", result.Errors.Select(e => e.Description)));
                }

                await users.AddToRoleAsync(user, Roles.Clerk);
            }

            if (user is not null)
            {
                clerks.Add(user);
            }
        }

        if (clerks.Count == 0)
        {
            clerks.AddRange(await db.Users.Where(u => u.IsActive).OrderBy(u => u.CreatedAt).ToListAsync());
        }

        return clerks;
    }

    private async Task<SampleCase[]> LoadSampleCasesAsync()
    {
        var path = Path.Combine(environment.ContentRootPath, "Seed", "sample-cases.json");
        if (!File.Exists(path))
        {
            logger.LogWarning("Sample cases file {Path} not found; only fictional demo requests are seeded.", path);
            return [];
        }

        await using var stream = File.OpenRead(path);
        var file = await JsonSerializer.DeserializeAsync<SampleCaseFile>(stream, JsonOptions);
        return file?.Cases ?? [];
    }

    private void NumberAfter(IReadOnlyCollection<RequestPlan> numbered, IEnumerable<RequestPlan> plans)
    {
        var last = numbered.GroupBy(p => p.Year).ToDictionary(g => g.Key, g => g.Max(p => p.Sequence));
        foreach (var plan in plans)
        {
            plan.Sequence = last[plan.Year] = last.GetValueOrDefault(plan.Year) + 1;
            plan.CaseNumber = numbers.Format(plan.Sequence, plan.Year);
        }
    }

    private void AddRequest(RequestPlan plan, AppUser? clerk, Dictionary<string, Department> departments, DateOnly today)
    {
        var userId = clerk?.Id ?? Guid.Empty;
        var request = new InfoRequest
        {
            Id = Guid.CreateVersion7(),
            Year = plan.Year,
            SequenceNumber = plan.Sequence,
            CaseNumber = plan.CaseNumber,
            Subject = plan.Subject,
            Content = plan.Content,
            RequesterName = plan.RequesterName,
            RequesterContact = plan.RequesterContact,
            ReplyChannel = plan.Channel,
            ReceivedOn = plan.ReceivedOn,
            DueOn = plan.DueOn,
            ExtendedDueOn = plan.ExtendedDueOn,
            ExtensionDays = plan.ExtensionDays,
            ExtensionKind = plan.ExtensionKind,
            ExtensionReason = plan.ExtensionReason,
            RespondedOn = plan.RespondedOn,
            Status = plan.Status,
            AssigneeId = clerk?.Id,
            CreatedAt = At(plan.ReceivedOn, 9),
            UpdatedAt = At(plan.RespondedOn ?? Min(today, plan.ReceivedOn.AddDays(3)), 15),
        };
        Audit(clerk, request.CreatedAt, "request.created", request.Id, request.CaseNumber);

        var linked = new List<Department>();
        foreach (var link in plan.Links)
        {
            if (!departments.TryGetValue(link.Symbol, out var department))
            {
                continue;
            }

            linked.Add(department);
            request.Departments.Add(new RequestDepartment
            {
                DepartmentId = department.Id,
                AskedOn = link.AskedOn,
                AnsweredOn = link.AnsweredOn,
            });
            if (link.AskedOn is { } askedOn)
            {
                Audit(clerk, At(askedOn, 10), "request.department-updated", request.Id,
                    AuditDetails.DepartmentDates(link.Symbol, askedOn, null));
            }

            if (link is { Reply: { } reply, AskedOn: { } asked, AnsweredOn: { } answered })
            {
                request.Documents.Add(ReplyDocument(request, department, asked, answered, reply, userId));
                Audit(clerk, At(answered, 12), "document.uploaded", request.Id, $"Odpowiedź wydziału {link.Symbol} (TXT)");
            }
        }

        if (plan is { ExtendedDueOn: { } extendedDueOn, ExtensionKind: { } kind, ExtensionDays: { } days })
        {
            var letterDate = Min(today, ExtensionLetterDate(plan, kind));
            var letter = plan.ExtensionLetter ?? RequestLetters.ExtensionNotice(request, extendedDueOn, Organization, letterDate);
            var title = kind == ExtensionKind.Clarification ? "Doprecyzowanie" : "Kwerenda";
            request.Documents.Add(TextDocument(DocumentKind.Attachment, null, $"{title} - {request.CaseNumber}.txt", letter, At(letterDate, 11), userId));
            Audit(clerk, At(letterDate, 11), "request.extended", request.Id, AuditDetails.Extension(kind, extendedDueOn, days));
        }

        var draft = plan.Draft ?? (plan.Answer is { } answer ? ResponseLetter(request, linked, plan.RespondedOn ?? today, answer) : null);
        if (draft is not null)
        {
            request.Draft = new ResponseDraft
            {
                Content = draft,
                GeneratedByAi = plan.RespondedOn is null,
                UpdatedAt = At(plan.RespondedOn ?? today.AddDays(-1), 13),
                UpdatedById = userId,
            };
        }

        if (plan.RespondedOn is { } respondedOn)
        {
            Audit(clerk, At(respondedOn, 14), "request.status-changed", request.Id,
                AuditDetails.StatusChange(RequestStatus.ResponsePrepared, RequestStatus.ResponseSent)
                + $", data wysłania odpowiedzi: {RequestLetters.ShortDate(respondedOn)}");
        }
        else if (plan.Status == RequestStatus.ResponsePrepared)
        {
            Audit(clerk, At(today.AddDays(-1), 13), "request.status-changed", request.Id,
                AuditDetails.StatusChange(RequestStatus.QueriesSent, RequestStatus.ResponsePrepared));
        }

        db.Requests.Add(request);
    }

    private static DateOnly ExtensionLetterDate(RequestPlan plan, ExtensionKind kind) =>
        kind == ExtensionKind.Clarification
            ? Min(plan.DueOn, Deadlines.NextWorkingDayOnOrAfter(plan.ReceivedOn.AddDays(5)))
            : plan.DueOn;

    private RequestDocument ReplyDocument(
        InfoRequest request, Department department, DateOnly askedOn, DateOnly answeredOn, string reply, Guid userId)
    {
        var text = $"""
            {department.Name} ({department.Symbol})
            {LetterLayout.PlaceAndDate(Organization, answeredOn)}

            {Organization.UnitName}
            w miejscu

            Dotyczy: wniosku o udostępnienie informacji publicznej, znak sprawy {request.CaseNumber}

            W odpowiedzi na zapytanie z dnia {LetterLayout.LongDate(askedOn)} uprzejmie przekazuję następujące informacje.

            {reply}

            Z poważaniem
            z up. Dyrektora
            [podpis]
            """;
        return TextDocument(DocumentKind.DepartmentReply, department.Id,
            $"odpowiedz_{department.Symbol}_{request.SequenceNumber}_{request.Year}.txt", text, At(answeredOn, 12), userId);
    }

    private static RequestDocument TextDocument(
        DocumentKind kind, Guid? departmentId, string fileName, string text, DateTimeOffset uploadedAt, Guid userId)
    {
        var content = Encoding.UTF8.GetBytes(text);
        return new RequestDocument
        {
            Id = Guid.CreateVersion7(),
            Kind = kind,
            DepartmentId = departmentId,
            File = new StoredFile
            {
                FileName = fileName,
                ContentType = "text/plain; charset=utf-8",
                SizeBytes = content.LongLength,
                Content = content,
                Sha256 = Convert.ToHexStringLower(SHA256.HashData(content)),
                ExtractedText = text,
                CreatedAt = uploadedAt,
                CreatedById = userId,
            },
            UploadedAt = uploadedAt,
            UploadedById = userId,
        };
    }

    private string ResponseLetter(InfoRequest request, IReadOnlyList<Department> departments, DateOnly date, string answer)
    {
        var addressee = LetterLayout.GuessAddressee(request.RequesterName);
        var names = departments.Select(d => d.Name).ToList();
        var source = names.Count switch
        {
            0 => "",
            1 => $", na podstawie informacji przygotowanych przez komórkę merytoryczną, tj. {names[0]},",
            _ => $", na podstawie informacji przygotowanych przez komórki merytoryczne, tj. {string.Join(", ", names[..^1])} oraz {names[^1]},",
        };
        return string.Join("\n\n",
            LetterLayout.PlaceAndDate(Organization, date),
            request.CaseNumber,
            LetterLayout.Salutation(addressee),
            $"w odpowiedzi na wystosowany {LetterLayout.ByYou(addressee)} wniosek o udostępnienie informacji publicznej dotyczący: „{request.Subject}”{source} uprzejmie informuję, co następuje.",
            answer,
            LetterLayout.Closing(Organization));
    }

    private void Audit(AppUser? clerk, DateTimeOffset at, string action, Guid requestId, string details) =>
        db.AuditEntries.Add(new AuditEntry
        {
            At = at,
            UserId = clerk?.Id,
            UserName = clerk?.UserName ?? "system",
            Action = action,
            EntityType = nameof(InfoRequest),
            EntityId = requestId,
            Details = details,
        });

    private static RequestPlan PlanSample(SampleCase c)
    {
        var number = c.CaseNumber.Split('.');
        var askedOn = Min(c.RespondedOn, Deadlines.NextWorkingDayOnOrAfter(c.ReceivedOn.AddDays(1)));
        var answeredOn = Min(c.RespondedOn,
            Deadlines.NextWorkingDayOnOrAfter(askedOn.AddDays((c.RespondedOn.DayNumber - askedOn.DayNumber) * 2 / 3)));
        return new RequestPlan
        {
            Year = int.Parse(number[^1], CultureInfo.InvariantCulture),
            Sequence = int.Parse(number[^2], CultureInfo.InvariantCulture),
            CaseNumber = c.CaseNumber,
            Subject = c.Subject,
            Content = string.IsNullOrWhiteSpace(c.RequestText) ? c.Subject : c.RequestText.Trim(),
            RequesterName = c.RequesterName,
            RequesterContact = c.RequesterName?.Contains('@') == true ? c.RequesterName : null,
            Channel = ReplyChannel.Email,
            ReceivedOn = c.ReceivedOn,
            DueOn = c.DueOn,
            ExtendedDueOn = c.ExtendedDueOn,
            ExtensionDays = c.ExtendedDueOn is { } extended ? extended.DayNumber - c.DueOn.DayNumber : null,
            ExtensionKind = c.ExtendedDueOn is null ? null : c.ExtensionKind ?? ExtensionKind.Query,
            ExtensionReason = c.ExtendedDueOn is null ? null : SampleExtensionReason(c.ExtensionKind ?? ExtensionKind.Query),
            RespondedOn = c.RespondedOn,
            Status = RequestStatus.ResponseSent,
            Clerk = Math.Max(0, c.Assignee - 1),
            Draft = c.ResponseText,
            ExtensionLetter = c.ExtensionLetterText,
            Links = c.Departments.Select(symbol => new LinkPlan(symbol, askedOn, answeredOn, null)).ToList(),
        };
    }

    private static string SampleExtensionReason(ExtensionKind kind) => kind == ExtensionKind.Clarification
        ? "Wezwanie wnioskodawcy do doprecyzowania zakresu wniosku."
        : "Konieczność przeprowadzenia całościowej kwerendy w zakresie wnioskowanych danych.";

    private static RequestPlan PlanCurrent(CurrentCase c, DateOnly today)
    {
        var dueOn = today.AddDays(c.DueInDays);
        var receivedOn = dueOn.AddDays(-Deadlines.StatutoryDays);
        var askedOn = Min(today.AddDays(-1), Deadlines.NextWorkingDayOnOrAfter(receivedOn.AddDays(1)));
        var year = today.Year;
        return new RequestPlan
        {
            Year = receivedOn.Year,
            Subject = Fill(c.Subject, year),
            Content = Fill(c.Content, year),
            RequesterName = c.Requester,
            RequesterContact = c.Contact,
            Channel = c.Channel,
            ReceivedOn = receivedOn,
            DueOn = dueOn,
            ExtendedDueOn = c.ExtensionDays is { } days ? Deadlines.ExtendedDueDate(dueOn, days) : null,
            ExtensionDays = c.ExtensionDays,
            ExtensionKind = c.ExtensionDays is null ? null : ExtensionKind.Query,
            ExtensionReason = c.ExtensionReason,
            Status = c.Status,
            Clerk = c.Clerk,
            Answer = c.DraftAnswer is null ? null : Fill(c.DraftAnswer, year),
            Links = c.Links.Select(link => new LinkPlan(
                link.Symbol,
                askedOn,
                link.AnsweredAfterDays is { } after ? Min(today, Deadlines.NextWorkingDayOnOrAfter(askedOn.AddDays(after))) : null,
                link.Reply is null ? null : Fill(link.Reply, year))).ToList(),
        };
    }

    private static RequestPlan PlanArchived(ArchivedCase c, DateOnly today)
    {
        var receivedOn = today.AddDays(-c.ReceivedDaysAgo);
        var dueOn = Deadlines.InitialDueDate(receivedOn);
        DateOnly? extendedDueOn = c.ExtensionDays is { } days ? Deadlines.ExtendedDueDate(dueOn, days) : null;
        var askedOn = Deadlines.NextWorkingDayOnOrAfter(receivedOn.AddDays(1));
        var links = c.Departments.Select((symbol, index) => new LinkPlan(
                symbol,
                askedOn,
                Deadlines.NextWorkingDayOnOrAfter(askedOn.AddDays(DepartmentReplyDays.GetValueOrDefault(symbol, 4) + (c.ReceivedDaysAgo + index) % 3 - 1)),
                null))
            .ToList();
        var lastAnswer = links.Count == 0 ? receivedOn : links.Max(l => l.AnsweredOn!.Value);
        var respondedOn = Deadlines.NextWorkingDayOnOrAfter(Max(receivedOn.AddDays(c.ResponseDays), lastAnswer.AddDays(1)));
        var year = receivedOn.Year;
        return new RequestPlan
        {
            Year = year,
            Subject = Fill(c.Subject, year),
            Content = Fill(c.Content, year),
            RequesterName = c.Requester,
            RequesterContact = c.Contact,
            Channel = c.Channel,
            ReceivedOn = receivedOn,
            DueOn = dueOn,
            ExtendedDueOn = extendedDueOn,
            ExtensionDays = c.ExtensionDays,
            ExtensionKind = c.ExtensionDays is null ? null : ExtensionKind.Query,
            ExtensionReason = c.ExtensionReason is null ? null : Fill(c.ExtensionReason, year),
            RespondedOn = Min(respondedOn, extendedDueOn ?? dueOn),
            Status = RequestStatus.ResponseSent,
            Clerk = c.ReceivedDaysAgo,
            Answer = Fill(c.Answer, year),
            Links = links,
        };
    }

    private static string Fill(string text, int year) => text
        .Replace("{Y-5}", (year - 5).ToString(CultureInfo.InvariantCulture))
        .Replace("{Y-2}", (year - 2).ToString(CultureInfo.InvariantCulture))
        .Replace("{Y-1}", (year - 1).ToString(CultureInfo.InvariantCulture))
        .Replace("{Y+1}", (year + 1).ToString(CultureInfo.InvariantCulture))
        .Replace("{Y}", year.ToString(CultureInfo.InvariantCulture));

    private static DateTimeOffset At(DateOnly date, int hour) =>
        new(TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(new TimeOnly(hour, 0)), Warsaw), TimeSpan.Zero);

    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;

    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;

    private sealed record SampleCaseFile(SampleCase[] Cases);

    private sealed record SampleCase(
        string CaseNumber,
        string? RequesterName,
        string Subject,
        DateOnly ReceivedOn,
        DateOnly DueOn,
        DateOnly? ExtendedDueOn,
        ExtensionKind? ExtensionKind,
        DateOnly RespondedOn,
        int Assignee,
        string[] Departments,
        string? RequestText,
        string? ResponseText,
        string? ExtensionLetterText);

    private sealed record CaseLink(string Symbol, int? AnsweredAfterDays = null, string? Reply = null);

    private sealed record CurrentCase(
        string Subject,
        string Content,
        string Requester,
        string Contact,
        ReplyChannel Channel,
        int DueInDays,
        RequestStatus Status,
        int Clerk,
        CaseLink[] Links,
        int? ExtensionDays = null,
        string? ExtensionReason = null,
        string? DraftAnswer = null);

    private sealed record ArchivedCase(
        string Subject,
        string Content,
        string Requester,
        string Contact,
        ReplyChannel Channel,
        int ReceivedDaysAgo,
        int ResponseDays,
        string[] Departments,
        string Answer,
        int? ExtensionDays = null,
        string? ExtensionReason = null);

    private sealed record LinkPlan(string Symbol, DateOnly? AskedOn, DateOnly? AnsweredOn, string? Reply);

    private sealed class RequestPlan
    {
        public int Year { get; init; }
        public int Sequence { get; set; }
        public string CaseNumber { get; set; } = "";
        public required string Subject { get; init; }
        public required string Content { get; init; }
        public string? RequesterName { get; init; }
        public string? RequesterContact { get; init; }
        public ReplyChannel Channel { get; init; }
        public DateOnly ReceivedOn { get; init; }
        public DateOnly DueOn { get; init; }
        public DateOnly? ExtendedDueOn { get; init; }
        public int? ExtensionDays { get; init; }
        public ExtensionKind? ExtensionKind { get; init; }
        public string? ExtensionReason { get; init; }
        public DateOnly? RespondedOn { get; init; }
        public RequestStatus Status { get; init; }
        public int Clerk { get; init; }
        public string? Answer { get; init; }
        public string? Draft { get; init; }
        public string? ExtensionLetter { get; init; }
        public required List<LinkPlan> Links { get; init; }
    }

    private static CurrentCase[] CurrentCases() =>
    [
        new(
            "Koszt organizacji miejskiej imprezy sylwestrowej {Y-1}/{Y}",
            "Na podstawie art. 2 ust. 1 ustawy z dnia 6 września 2001 r. o dostępie do informacji publicznej wnoszę o udostępnienie informacji o łącznym koszcie organizacji miejskiej imprezy sylwestrowej na Starym Rynku w Bydgoszczy w dniu 31 grudnia {Y-1} r. "
            + "Proszę o wskazanie kosztów w podziale na: honoraria artystów, obsługę techniczną (scena, nagłośnienie, oświetlenie), pokaz multimedialny i pirotechniczny, ochronę i zabezpieczenie medyczne oraz promocję wydarzenia. "
            + "Proszę również o informację, czy wydarzenie było współfinansowane przez sponsorów, a jeśli tak – w jakiej kwocie. "
            + "Informację proszę przesłać w formie elektronicznej na podany adres e-mail.",
            "Tomasz Wróblewski",
            "t.wroblewski@example.com",
            ReplyChannel.Email,
            DueInDays: -2,
            RequestStatus.QueriesSent,
            Clerk: 0,
            [new("BKB"), new("WZP", AnsweredAfterDays: 4)]),
        new(
            "Liczba drzew wyciętych i nasadzonych na terenie miasta w {Y} r.",
            "Wnoszę o udostępnienie informacji publicznej dotyczącej gospodarki drzewostanem w Bydgoszczy. "
            + "Proszę o podanie liczby drzew usuniętych z terenów należących do Miasta od 1 stycznia {Y} r. do dnia udzielenia odpowiedzi, z podziałem na usunięcia związane z inwestycjami oraz usunięcia ze względu na zły stan zdrowotny drzew. "
            + "Proszę również o informację, ile drzew posadzono w tym okresie w ramach nasadzeń zastępczych i kompensacyjnych oraz jaki był koszt tych nasadzeń. "
            + "Odpowiedź proszę przesłać na adres e-mail, z którego wysłano wniosek.",
            "Katarzyna Lewandowska",
            "k.lewandowska@example.com",
            ReplyChannel.Email,
            DueInDays: 0,
            RequestStatus.QueriesSent,
            Clerk: 1,
            [
                new("WGK"),
                new("WOŚ", AnsweredAfterDays: 6, Reply:
                    "1. W okresie od 1 stycznia do 31 października {Y} r. wydano 47 decyzji zezwalających na usunięcie łącznie 612 drzew z nieruchomości stanowiących własność Miasta.\n"
                    + "2. Z ogólnej liczby usuniętych drzew 389 usunięto w związku z realizacją inwestycji drogowych i kubaturowych, a 223 ze względu na zły stan zdrowotny lub zagrożenie bezpieczeństwa.\n"
                    + "3. W decyzjach nałożono obowiązek wykonania nasadzeń zastępczych 1 140 drzew. Do dnia sporządzenia informacji wykonano 730 nasadzeń, pozostałe zaplanowano na sezon jesienny.\n"
                    + "4. Informacjami o kosztach nasadzeń realizowanych przez Miasto dysponuje Wydział Zieleni i Gospodarki Komunalnej."),
            ]),
        new(
            "Wartość umowy na mechaniczne oczyszczanie ulic",
            "Proszę o udostępnienie informacji o umowie lub umowach zawartych przez Miasto Bydgoszcz na mechaniczne oczyszczanie ulic, chodników i placów w latach {Y-1}–{Y}. "
            + "Wnoszę o podanie nazwy wykonawcy, okresu obowiązywania umowy, jej wartości brutto oraz kwot faktycznie wypłaconych wykonawcy w {Y-1} r. i w bieżącym roku. "
            + "Proszę także o informację, w jakim trybie udzielono zamówienia i ile ofert złożono w postępowaniu. "
            + "Informacje zostaną wykorzystane w materiale prasowym o kosztach utrzymania czystości w mieście.",
            "Paweł Szymański (redakcja „Kurier Fordoński”)",
            "redakcja@kurier-fordonski.example.org",
            ReplyChannel.Email,
            DueInDays: 0,
            RequestStatus.ResponsePrepared,
            Clerk: 2,
            [
                new("WGK", AnsweredAfterDays: 5, Reply:
                    "Umowa nr WGK/{Y-1}/018 na mechaniczne oczyszczanie ulic, chodników i placów została zawarta w dniu 15 lutego {Y-1} r. z „Miejskie Usługi Porządkowe” sp. z o.o. na okres od 1 marca {Y-1} r. do 28 lutego {Y+1} r.\n"
                    + "Wartość umowy wynosi 18 450 000,00 zł brutto.\n"
                    + "Z tytułu realizacji umowy wypłacono wykonawcy: w {Y-1} r. – 7 212 340,55 zł brutto, w {Y} r. (do 31 października) – 6 104 870,20 zł brutto."),
                new("WZP", AnsweredAfterDays: 3, Reply:
                    "Zamówienie zostało udzielone w trybie przetargu nieograniczonego (postępowanie nr WZP.271.12.{Y-2}). Ogłoszenie o zamówieniu opublikowano w Dzienniku Urzędowym Unii Europejskiej w dniu 2 listopada {Y-2} r.\n"
                    + "W postępowaniu złożono 3 oferty. Najkorzystniejszą ofertę złożyła spółka „Miejskie Usługi Porządkowe” sp. z o.o.\n"
                    + "Dokumentacja postępowania jest dostępna na platformie zakupowej Miasta."),
            ],
            DraftAnswer:
                "1. Umowę na mechaniczne oczyszczanie ulic, chodników i placów zawarto w dniu 15 lutego {Y-1} r. ze spółką „Miejskie Usługi Porządkowe” sp. z o.o. na okres od 1 marca {Y-1} r. do 28 lutego {Y+1} r.\n\n"
                + "2. Wartość umowy wynosi 18 450 000,00 zł brutto. Z tytułu jej realizacji wypłacono wykonawcy w {Y-1} r. kwotę 7 212 340,55 zł brutto, a w {Y} r. (do 31 października) kwotę 6 104 870,20 zł brutto.\n\n"
                + "3. Zamówienie zostało udzielone w trybie przetargu nieograniczonego (postępowanie nr WZP.271.12.{Y-2}). W postępowaniu złożono 3 oferty. Dokumentacja postępowania jest dostępna na platformie zakupowej Miasta."),
        new(
            "Wydatki na remonty szkół podstawowych w {Y} r.",
            "Wnoszę o udostępnienie informacji o wydatkach Miasta Bydgoszczy na remonty i modernizację budynków szkół podstawowych w {Y} r. "
            + "Proszę o zestawienie obejmujące nazwę szkoły, zakres prac (np. termomodernizacja, wymiana instalacji, remont sanitariatów, boiska) oraz kwotę wydatkowaną lub zaplanowaną w budżecie. "
            + "Proszę również o wskazanie, które z tych zadań były finansowane ze środków zewnętrznych. "
            + "Informację proszę przekazać w formie arkusza kalkulacyjnego lub pliku PDF.",
            "Agnieszka Dąbrowska",
            "ul. Fordońska 210/12, 85-790 Bydgoszcz",
            ReplyChannel.Post,
            DueInDays: 1,
            RequestStatus.QueriesSent,
            Clerk: 0,
            [
                new("WE", AnsweredAfterDays: 4, Reply:
                    "1. W budżecie Miasta na {Y} r. na remonty bieżące szkół podstawowych zaplanowano 9 850 000 zł; do 31 października wydatkowano 7 326 410 zł.\n"
                    + "2. Największe zadania: remont sanitariatów w 12 szkołach – 2 140 000 zł, wymiana instalacji elektrycznej w 4 szkołach – 1 580 000 zł, remont sal gimnastycznych w 3 szkołach – 1 210 000 zł, modernizacja boisk szkolnych w 5 szkołach – 1 960 000 zł.\n"
                    + "3. Zestawienie w podziale na poszczególne szkoły możemy przekazać w formie arkusza kalkulacyjnego.\n"
                    + "4. Zadania inwestycyjne, w tym termomodernizację budynków szkolnych współfinansowaną ze środków zewnętrznych, realizuje Wydział Inwestycji Miasta."),
                new("WIM"),
            ]),
        new(
            "Długość dróg rowerowych wybudowanych w latach {Y-2}–{Y}",
            "Jako członek stowarzyszenia działającego na rzecz komunikacji rowerowej wnoszę o udostępnienie informacji, ile kilometrów nowych dróg dla rowerów oraz ciągów pieszo-rowerowych wybudowano w Bydgoszczy w latach {Y-2}, {Y-1} i {Y}, z podziałem na poszczególne lata. "
            + "Proszę także o podanie łącznej długości infrastruktury rowerowej w mieście według stanu na dzień udzielenia odpowiedzi. "
            + "Proszę również o informację o planowanych na {Y+1} r. inwestycjach rowerowych wraz z kwotami zapisanymi w budżecie lub Wieloletniej Prognozie Finansowej.",
            "Łukasz Pawłowski (Stowarzyszenie Rowerowe „Szprycha”)",
            "l.pawlowski@example.org",
            ReplyChannel.Email,
            DueInDays: 2,
            RequestStatus.NoAction,
            Clerk: 1,
            []),
        new(
            "Wpływy ze Strefy Płatnego Parkowania w {Y-1} r.",
            "Proszę o udostępnienie informacji o wysokości wpływów do budżetu Miasta Bydgoszczy z tytułu opłat za postój w Strefie Płatnego Parkowania w {Y-1} r. oraz w okresie od stycznia do września {Y} r. "
            + "Wnoszę o podanie osobno kwot z opłat zwykłych, abonamentów i identyfikatorów oraz z opłat dodatkowych nałożonych za brak biletu. "
            + "Proszę również o informację, na jakie cele przeznaczono te środki. "
            + "Odpowiedź proszę przesłać na skrzynkę ePUAP Fundacji.",
            "Fundacja „Jawne Miasto”",
            "ePUAP: /JawneMiasto/SkrytkaESP",
            ReplyChannel.Epuap,
            DueInDays: 3,
            RequestStatus.QueriesSent,
            Clerk: 2,
            [new("WZB"), new("WW")]),
        new(
            "Liczba osób oczekujących na przydział mieszkania komunalnego",
            "Wnoszę o udostępnienie informacji, ile gospodarstw domowych oczekuje obecnie na przydział lokalu mieszkalnego z mieszkaniowego zasobu Miasta Bydgoszczy, z podziałem na lokale komunalne i socjalne. "
            + "Proszę o podanie, ile lokali przydzielono w {Y-1} r. i w bieżącym roku oraz jaki jest średni czas oczekiwania na przydział. "
            + "Proszę także o informację, ile pustostanów jest obecnie w remoncie lub oczekuje na remont.",
            "Grzegorz Piotrowski",
            "ul. Nakielska 85/3, 85-391 Bydgoszcz",
            ReplyChannel.Post,
            DueInDays: 6,
            RequestStatus.NoAction,
            Clerk: 0,
            []),
        new(
            "Koszty usuwania graffiti z obiektów miejskich",
            "Proszę o informację, jakie kwoty Miasto Bydgoszcz przeznaczyło na usuwanie graffiti i nielegalnych napisów z elewacji budynków, wiat przystankowych i innych obiektów miejskich w {Y-1} r. i w {Y} r. "
            + "Proszę o podanie powierzchni oczyszczonej (w m²) oraz informacji, kto wykonuje te prace. "
            + "Interesuje mnie również, ile zgłoszeń dotyczących graffiti wpłynęło w tym okresie i czy Miasto prowadzi program legalnych ścian do malowania.",
            "Monika Krawczyk",
            "m.krawczyk@example.com",
            ReplyChannel.Email,
            DueInDays: 9,
            RequestStatus.QueriesSent,
            Clerk: 1,
            [new("WGK")]),
        new(
            "Dotacje dla organizacji pozarządowych w otwartych konkursach ofert w {Y} r.",
            "Na podstawie ustawy o dostępie do informacji publicznej wnoszę o przekazanie wykazu dotacji udzielonych organizacjom pozarządowym przez Miasto Bydgoszcz w {Y} r. w trybie otwartych konkursów ofert oraz w trybie pozakonkursowym (tzw. małe granty). "
            + "Wykaz powinien zawierać nazwę organizacji, tytuł zadania, obszar (np. kultura, sport, pomoc społeczna) oraz kwotę dotacji. "
            + "Proszę również o informację o łącznej kwocie dotacji w poszczególnych obszarach w porównaniu z {Y-1} r.",
            "Natalia Michalska",
            "n.michalska@example.org",
            ReplyChannel.Email,
            DueInDays: 12,
            RequestStatus.NoAction,
            Clerk: 2,
            []),
        new(
            "Liczba pozwoleń na budowę wydanych w latach {Y-5}–{Y}",
            "Wnoszę o udostępnienie informacji o liczbie decyzji o pozwoleniu na budowę wydanych przez Prezydenta Miasta Bydgoszczy w latach {Y-5}–{Y}, w podziale na poszczególne lata oraz na budynki mieszkalne jednorodzinne, wielorodzinne i pozostałe obiekty. "
            + "Proszę także o podanie liczby decyzji odmownych oraz liczby przyjętych zgłoszeń budowy domów jednorodzinnych o powierzchni zabudowy do 70 m². "
            + "Dane posłużą do przygotowania pracy dyplomowej o rozwoju budownictwa mieszkaniowego w Bydgoszczy.",
            "Jakub Król",
            "j.krol@example.com",
            ReplyChannel.Email,
            DueInDays: -4,
            RequestStatus.Extended,
            Clerk: 0,
            [new("WAB")],
            ExtensionDays: 21,
            ExtensionReason: "Konieczność przeprowadzenia kwerendy w archiwum zakładowym i zestawienia danych z rejestrów prowadzonych w latach objętych wnioskiem w różnych systemach informatycznych."),
    ];

    private static ArchivedCase[] ArchivedCases() =>
    [
        new(
            "Koszty organizacji Sylwestra Miejskiego na Starym Rynku",
            "Zwracam się z prośbą o udostępnienie informacji, ile kosztowała Miasto Bydgoszcz organizacja zabawy sylwestrowej na Starym Rynku w nocy z 31 grudnia {Y-1} r. na 1 stycznia {Y} r. "
            + "Proszę o podanie kosztów występów artystycznych, wynajmu sceny i nagłośnienia, pokazu pirotechnicznego oraz zabezpieczenia imprezy. "
            + "Czy Miasto pozyskało na ten cel środki od sponsorów?",
            "Marek Mazur",
            "marek.mazur@example.com",
            ReplyChannel.Email,
            ReceivedDaysAgo: 300,
            ResponseDays: 12,
            ["BKB", "WZP"],
            "Łączny koszt organizacji imprezy sylwestrowej na Starym Rynku wyniósł 612 400,00 zł brutto, w tym:\n"
            + "– honoraria artystów – 268 000,00 zł,\n"
            + "– scena, nagłośnienie i oświetlenie – 154 900,00 zł,\n"
            + "– pokaz multimedialny i pirotechniczny – 98 500,00 zł,\n"
            + "– ochrona i zabezpieczenie medyczne – 61 300,00 zł,\n"
            + "– promocja wydarzenia – 29 700,00 zł.\n\n"
            + "Wydarzenie zostało częściowo sfinansowane przez sponsorów, którzy przekazali łącznie 85 000,00 zł. Organizatorem wydarzenia było Miasto Bydgoszcz we współpracy z miejską instytucją kultury."),
        new(
            "Wycinka drzew w związku z inwestycjami drogowymi w {Y-1} r.",
            "Proszę o informację, ile drzew usunięto w {Y-1} r. w Bydgoszczy w związku z realizacją miejskich inwestycji drogowych i tramwajowych. "
            + "Proszę o wskazanie inwestycji, których dotyczyły wycinki, oraz liczby drzew posadzonych w ramach nasadzeń zastępczych. "
            + "Czy Miasto prowadzi rejestr nasadzeń kompensacyjnych dostępny dla mieszkańców?",
            "Ewa Jankowska",
            "ewa.jankowska@example.org",
            ReplyChannel.Email,
            ReceivedDaysAgo: 250,
            ResponseDays: 13,
            ["WOŚ", "WGK"],
            "W {Y-1} r. w związku z realizacją miejskich inwestycji drogowych i tramwajowych usunięto łącznie 534 drzewa, w tym 212 przy przebudowie układu drogowego w rejonie Fordonu, 148 przy budowie nowego odcinka trasy tramwajowej oraz 174 przy pozostałych inwestycjach.\n\n"
            + "W ramach nasadzeń zastępczych posadzono 1 086 drzew, głównie w parkach miejskich oraz wzdłuż przebudowanych ciągów komunikacyjnych.\n\n"
            + "Informacje o wykonanych nasadzeniach są publikowane na stronie internetowej Miasta w zakładce poświęconej zieleni miejskiej."),
        new(
            "Koszt zimowego utrzymania i oczyszczania ulic w sezonie {Y-2}/{Y-1}",
            "Wnoszę o udostępnienie informacji o kosztach utrzymania czystości na drogach publicznych w Bydgoszczy, w tym zimowego utrzymania dróg, w sezonie {Y-2}/{Y-1}. "
            + "Proszę o podanie wartości umów zawartych z wykonawcami, nazw wykonawców oraz kwot faktycznie zapłaconych. "
            + "Proszę również o informację, ile razy w sezonie prowadzono akcję odśnieżania.",
            "Krzysztof Kamiński",
            "ul. Grunwaldzka 64/8, 85-239 Bydgoszcz",
            ReplyChannel.Post,
            ReceivedDaysAgo: 200,
            ResponseDays: 11,
            ["WGK", "WZP"],
            "Zimowe utrzymanie dróg w sezonie {Y-2}/{Y-1} prowadzone było na podstawie dwóch umów zawartych po przeprowadzeniu przetargów nieograniczonych, o łącznej wartości 14 870 000,00 zł brutto. "
            + "Wykonawcom wypłacono łącznie 11 236 500,00 zł brutto, ponieważ część usług rozliczana jest według faktycznie wykonanych prac.\n\n"
            + "W sezonie przeprowadzono 23 akcje zimowe, w tym 9 akcji odśnieżania i 14 akcji prewencyjnego posypywania jezdni.\n\n"
            + "Nazwy wykonawców oraz treść umów są dostępne w rejestrze umów publikowanym w Biuletynie Informacji Publicznej."),
        new(
            "Termomodernizacja i remonty szkół w {Y-1} r.",
            "Proszę o udostępnienie zestawienia remontów i inwestycji przeprowadzonych w {Y-1} r. w szkołach podstawowych i przedszkolach prowadzonych przez Miasto Bydgoszcz. "
            + "W zestawieniu proszę uwzględnić nazwę placówki, rodzaj prac oraz poniesione koszty. "
            + "Proszę także o informację, jakie środki pozyskano na ten cel z funduszy zewnętrznych.",
            "Joanna Woźniak",
            "j.wozniak@example.com",
            ReplyChannel.Email,
            ReceivedDaysAgo: 180,
            ResponseDays: 12,
            ["WE", "WIM"],
            "W {Y-1} r. Miasto przeznaczyło na remonty bieżące szkół podstawowych i przedszkoli 8 940 000 zł, a na zadania inwestycyjne, w tym termomodernizację 6 budynków szkolnych, 21 350 000 zł. "
            + "Termomodernizacja była współfinansowana ze środków Unii Europejskiej w kwocie 11 200 000 zł.\n\n"
            + "Najwięcej środków przeznaczono na remonty sanitariatów (18 placówek), wymianę stolarki okiennej (9 placówek) oraz modernizację boisk (4 placówki).\n\n"
            + "Zestawienie w podziale na poszczególne placówki przekazujemy w załączeniu w formie arkusza kalkulacyjnego."),
        new(
            "Infrastruktura rowerowa w Bydgoszczy – kilometry nowych tras",
            "Wnoszę o informację, ile kilometrów dróg rowerowych powstało w Bydgoszczy w {Y-1} r. oraz jaka jest łączna długość tras rowerowych w mieście. "
            + "Proszę o wymienienie najważniejszych nowych odcinków i ich kosztu. "
            + "Czy Miasto planuje budowę kolejnych tras w najbliższych latach?",
            "Rafał Nowicki",
            "rafal.nowicki@example.com",
            ReplyChannel.Email,
            ReceivedDaysAgo: 150,
            ResponseDays: 10,
            ["WIM"],
            "W {Y-1} r. w Bydgoszczy oddano do użytku 11,8 km nowych dróg dla rowerów i ciągów pieszo-rowerowych, w tym 7,2 km w ramach przebudowy dróg i 4,6 km w ramach zadań wyłącznie rowerowych. "
            + "Łączny koszt zadań wyłącznie rowerowych wyniósł 9 340 000 zł.\n\n"
            + "Łączna długość infrastruktury rowerowej w mieście na koniec {Y-1} r. wynosiła 187,4 km.\n\n"
            + "W Wieloletniej Prognozie Finansowej na lata {Y}–{Y+1} zaplanowano budowę kolejnych 15 km tras rowerowych."),
        new(
            "Dochody z opłat parkingowych i opłat dodatkowych w strefie płatnego parkowania",
            "Proszę o informację o dochodach Miasta Bydgoszczy ze Strefy Płatnego Parkowania w {Y-1} r., w tym o kwocie z opłat dodatkowych za nieuiszczenie opłaty. "
            + "Proszę również o podanie, ile zawiadomień o opłacie dodatkowej wystawiono i jaka część z nich została opłacona. "
            + "Interesuje mnie także, ile spraw skierowano do egzekucji.",
            "Dariusz Dudek",
            "d.dudek@example.org",
            ReplyChannel.Email,
            ReceivedDaysAgo: 120,
            ResponseDays: 9,
            ["WZB", "WW"],
            "Dochody Miasta z tytułu opłat za postój w Strefie Płatnego Parkowania w {Y-1} r. wyniosły 16 482 300 zł, w tym:\n"
            + "– opłaty zwykłe – 12 905 100 zł,\n"
            + "– abonamenty i identyfikatory – 1 214 600 zł,\n"
            + "– opłaty dodatkowe – 2 362 600 zł.\n\n"
            + "W {Y-1} r. wystawiono 41 230 zawiadomień o opłacie dodatkowej, z czego 29 870 zostało opłaconych dobrowolnie. Do egzekucji administracyjnej skierowano 6 940 tytułów wykonawczych.\n\n"
            + "Dochody ze strefy przeznaczane są na zadania z zakresu utrzymania dróg i rozwoju transportu zbiorowego."),
        new(
            "Przydziały mieszkań komunalnych i lista oczekujących",
            "Wnoszę o udostępnienie informacji, ile mieszkań komunalnych i socjalnych Miasto Bydgoszcz przydzieliło w {Y-1} r. oraz ile osób oczekuje na przydział lokalu. "
            + "Proszę o informację o średnim czasie oczekiwania na mieszkanie oraz o liczbie lokali wyremontowanych w tym czasie.",
            "Barbara Adamczyk",
            "ul. Szubińska 41/2, 85-312 Bydgoszcz",
            ReplyChannel.Post,
            ReceivedDaysAgo: 100,
            ResponseDays: 13,
            ["WMG", "BZS"],
            "W {Y-1} r. przydzielono 214 lokali z mieszkaniowego zasobu Miasta, w tym 152 lokale komunalne i 62 lokale socjalne.\n\n"
            + "Według stanu na 31 grudnia {Y-1} r. na listach osób uprawnionych do zawarcia umowy najmu znajdowało się 1 385 gospodarstw domowych (812 – lokal komunalny, 573 – lokal socjalny).\n\n"
            + "Średni czas oczekiwania na przydział lokalu wynosił około 3,5 roku dla lokali komunalnych i 2 lata dla lokali socjalnych. W {Y-1} r. wyremontowano 168 pustostanów."),
        new(
            "Usuwanie graffiti – wydatki Miasta",
            "Proszę o podanie kwot wydanych przez Miasto Bydgoszcz w {Y-1} r. na usuwanie graffiti z budynków komunalnych, przystanków i mostów. "
            + "Ile metrów kwadratowych powierzchni oczyszczono? "
            + "Kto jest wykonawcą tych prac i czy mieszkańcy mogą zgłaszać nowe napisy?",
            "Wojciech Wieczorek",
            "w.wieczorek@example.com",
            ReplyChannel.Email,
            ReceivedDaysAgo: 90,
            ResponseDays: 8,
            ["WGK"],
            "W {Y-1} r. na usuwanie graffiti i nielegalnych napisów z obiektów miejskich wydatkowano 486 200 zł brutto.\n\n"
            + "Oczyszczono łącznie 9 870 m² powierzchni, w tym elewacje budynków komunalnych (5 420 m²), wiaty przystankowe (1 960 m²) oraz obiekty mostowe i ekrany akustyczne (2 490 m²).\n\n"
            + "Prace wykonuje wykonawca wyłoniony w przetargu nieograniczonym, w ramach umowy na utrzymanie czystości obiektów małej architektury. "
            + "Mieszkańcy mogą zgłaszać graffiti za pomocą formularza na stronie internetowej Miasta – w {Y-1} r. przyjęto 1 214 takich zgłoszeń."),
        new(
            "Wykaz dotacji dla organizacji pozarządowych w {Y-1} r.",
            "Wnoszę o udostępnienie informacji o łącznej kwocie dotacji przekazanych organizacjom pozarządowym przez Miasto Bydgoszcz w {Y-1} r., w podziale na obszary zadań publicznych. "
            + "Proszę także o informację, ile ofert złożono w otwartych konkursach i ile z nich otrzymało dofinansowanie.",
            "Fundacja „Jawne Miasto”",
            "ePUAP: /JawneMiasto/SkrytkaESP",
            ReplyChannel.Epuap,
            ReceivedDaysAgo: 80,
            ResponseDays: 10,
            ["BAS", "WZB"],
            "W {Y-1} r. Miasto Bydgoszcz przekazało organizacjom pozarządowym dotacje w łącznej kwocie 38 640 000 zł, w tym:\n"
            + "– pomoc społeczna i przeciwdziałanie wykluczeniu – 14 210 000 zł,\n"
            + "– sport i kultura fizyczna – 9 870 000 zł,\n"
            + "– kultura i ochrona dziedzictwa – 4 950 000 zł,\n"
            + "– ochrona zdrowia – 3 420 000 zł,\n"
            + "– edukacja i wypoczynek dzieci – 3 160 000 zł,\n"
            + "– pozostałe obszary – 3 030 000 zł.\n\n"
            + "W otwartych konkursach ofert złożono 642 oferty, dofinansowanie otrzymało 418 z nich. W trybie pozakonkursowym (art. 19a ustawy o działalności pożytku publicznego i o wolontariacie) udzielono 57 dotacji. "
            + "Wykaz dotacji wraz z nazwami organizacji jest publikowany w Biuletynie Informacji Publicznej."),
        new(
            "Liczba pozwoleń na budowę wydanych w {Y-1} r.",
            "Proszę o informację, ile decyzji o pozwoleniu na budowę wydał Prezydent Miasta Bydgoszczy w {Y-1} r., w tym dla budynków mieszkalnych wielorodzinnych i jednorodzinnych. "
            + "Proszę również o podanie liczby lokali mieszkalnych objętych tymi decyzjami oraz liczby decyzji odmownych.",
            "Karolina Jabłońska",
            "k.jablonska@example.com",
            ReplyChannel.Email,
            ReceivedDaysAgo: 70,
            ResponseDays: 24,
            ["WAB"],
            "W {Y-1} r. Prezydent Miasta Bydgoszczy wydał 1 126 decyzji o pozwoleniu na budowę, w tym 74 dla budynków mieszkalnych wielorodzinnych (obejmujących łącznie 3 912 lokali mieszkalnych) oraz 389 dla budynków mieszkalnych jednorodzinnych.\n\n"
            + "Wydano 41 decyzji odmownych.\n\n"
            + "Przyjęto ponadto 512 zgłoszeń robót budowlanych, w tym 63 zgłoszenia budowy domów jednorodzinnych o powierzchni zabudowy do 70 m².",
            ExtensionDays: 14,
            ExtensionReason: "Konieczność zestawienia danych z rejestru decyzji i rejestru zgłoszeń prowadzonych w odrębnych systemach."),
        new(
            "Wydatki na promocję miasta w mediach społecznościowych",
            "Wnoszę o udostępnienie informacji o kwotach wydatkowanych przez Urząd Miasta Bydgoszczy w {Y-1} r. na promocję miasta w mediach społecznościowych, w tym na płatne kampanie reklamowe. "
            + "Proszę o podanie nazw wykonawców i wartości umów.",
            "Magdalena Zając",
            "m.zajac@example.org",
            ReplyChannel.Email,
            ReceivedDaysAgo: 60,
            ResponseDays: 9,
            ["BP", "WZP"],
            "W {Y-1} r. na promocję miasta w mediach społecznościowych wydatkowano 742 500 zł brutto, w tym 318 000 zł na płatne kampanie reklamowe i 424 500 zł na przygotowanie treści (fotografie, filmy, grafiki).\n\n"
            + "Usługi realizowane były na podstawie 3 umów zawartych w trybie podstawowym, a kampanie reklamowe rozliczano bezpośrednio z operatorami platform.\n\n"
            + "Wykaz umów wraz z nazwami wykonawców jest dostępny w rejestrze umów publikowanym w Biuletynie Informacji Publicznej."),
        new(
            "Liczba kamer miejskiego monitoringu i koszty jego utrzymania",
            "Proszę o informację, ile kamer obejmuje miejski system monitoringu wizyjnego w Bydgoszczy, ile z nich zainstalowano w {Y-1} r. i jaki był koszt utrzymania systemu w {Y-1} r. "
            + "Czy nagrania są przekazywane Policji i ile razy w roku to następowało?",
            "Jakub Król",
            "j.krol@example.com",
            ReplyChannel.Email,
            ReceivedDaysAgo: 45,
            ResponseDays: 12,
            ["WZK", "BCB"],
            "Miejski system monitoringu wizyjnego obejmuje 386 kamer, z czego 42 zainstalowano w {Y-1} r.\n\n"
            + "Koszt utrzymania systemu w {Y-1} r. (serwis, łącza, energia, obsługa centrum monitoringu) wyniósł 2 134 000 zł brutto.\n\n"
            + "Nagrania udostępniane są Policji i innym uprawnionym organom na ich wniosek – w {Y-1} r. przekazano materiały w 1 027 przypadkach."),
        new(
            "Koszty utrzymania fontann miejskich",
            "Wnoszę o informację, ile fontann i kurtyn wodnych utrzymuje Miasto Bydgoszcz oraz jaki był łączny koszt ich utrzymania w sezonie {Y}, w tym koszt wody i energii elektrycznej. "
            + "Proszę także o informację, w jakich miesiącach fontanny są uruchamiane.",
            "Stanisław Olszewski",
            "ul. Gdańska 120/5, 85-022 Bydgoszcz",
            ReplyChannel.Post,
            ReceivedDaysAgo: 330,
            ResponseDays: 7,
            ["WGK"],
            "Miasto utrzymuje 14 fontann oraz 6 kurtyn wodnych.\n\n"
            + "Łączny koszt ich utrzymania w sezonie {Y} wyniósł 512 800 zł brutto, w tym: woda – 96 400 zł, energia elektryczna – 74 900 zł, konserwacja i naprawy – 341 500 zł.\n\n"
            + "Fontanny są uruchamiane co roku na przełomie kwietnia i maja i działają do końca września, w zależności od warunków pogodowych."),
        new(
            "Umorzenia zaległości w podatku od nieruchomości",
            "Proszę o udostępnienie informacji o łącznej kwocie umorzeń zaległości w podatku od nieruchomości udzielonych przez Prezydenta Miasta Bydgoszczy w {Y-1} r., z podziałem na osoby fizyczne i osoby prawne. "
            + "Proszę również o informację o liczbie decyzji o rozłożeniu zapłaty na raty i odroczeniu terminu płatności.",
            "Aleksandra Grabowska",
            "a.grabowska@example.com",
            ReplyChannel.Email,
            ReceivedDaysAgo: 280,
            ResponseDays: 11,
            ["WPL"],
            "W {Y-1} r. wydano 312 decyzji umarzających zaległości w podatku od nieruchomości na łączną kwotę 1 846 300 zł, w tym 268 decyzji dla osób fizycznych (412 700 zł) i 44 decyzje dla osób prawnych i jednostek organizacyjnych (1 433 600 zł).\n\n"
            + "Wydano ponadto 187 decyzji o rozłożeniu zapłaty podatku na raty i 39 decyzji o odroczeniu terminu płatności.\n\n"
            + "Wykaz osób prawnych i fizycznych prowadzących działalność gospodarczą, którym udzielono ulg w wysokości powyżej 500 zł, jest publikowany w Biuletynie Informacji Publicznej zgodnie z art. 37 ust. 1 pkt 2 lit. f ustawy o finansach publicznych."),
        new(
            "Budżet obywatelski – liczba projektów i stopień realizacji",
            "Wnoszę o udostępnienie informacji o Bydgoskim Budżecie Obywatelskim: ile projektów zgłoszono i ile wybrano do realizacji w edycji na {Y-1} r., jaka była łączna kwota przeznaczona na budżet obywatelski oraz ile projektów zostało już zrealizowanych.",
            "Kinga Jaworska",
            "k.jaworska@example.org",
            ReplyChannel.Email,
            ReceivedDaysAgo: 240,
            ResponseDays: 6,
            ["BAS"],
            "W edycji Bydgoskiego Budżetu Obywatelskiego na {Y-1} r. zgłoszono 287 projektów, z których 241 zostało dopuszczonych do głosowania.\n\n"
            + "Do realizacji wybrano 96 projektów o łącznej wartości 13 100 000 zł.\n\n"
            + "Według stanu na dzień udzielenia odpowiedzi zrealizowano 71 projektów, 18 jest w trakcie realizacji, a realizacja 7 projektów została przesunięta na kolejny rok z uwagi na konieczność uzyskania wymaganych pozwoleń."),
        new(
            "Koszty systemu gospodarowania odpadami komunalnymi",
            "Proszę o informację o kosztach funkcjonowania systemu gospodarowania odpadami komunalnymi w Bydgoszczy w {Y-1} r. oraz o wpływach z opłat od mieszkańców. "
            + "Proszę o podanie ilości odebranych odpadów zmieszanych i selektywnie zebranych w tonach.",
            "Michał Zieliński",
            "michal.zielinski@example.com",
            ReplyChannel.Email,
            ReceivedDaysAgo: 350,
            ResponseDays: 9,
            ["WGO"],
            "Koszt funkcjonowania systemu gospodarowania odpadami komunalnymi w {Y-1} r. wyniósł 118 420 000 zł, a wpływy z opłat za gospodarowanie odpadami komunalnymi – 112 960 000 zł. Różnica została pokryta z dochodów własnych Miasta.\n\n"
            + "Z nieruchomości zamieszkałych odebrano 74 380 ton odpadów zmieszanych oraz 52 140 ton odpadów zebranych selektywnie, w tym 18 960 ton odpadów biodegradowalnych."),
        new(
            "Dofinansowanie wymiany pieców węglowych",
            "Wnoszę o udostępnienie informacji, ile wniosków o dofinansowanie wymiany źródeł ciepła złożyli mieszkańcy Bydgoszczy w {Y-1} r. w ramach miejskiego programu ograniczania niskiej emisji, ile z nich rozpatrzono pozytywnie i jaka była łączna kwota dotacji. "
            + "Ile pieców węglowych zlikwidowano dzięki programowi?",
            "Zofia Majewska",
            "z.majewska@example.com",
            ReplyChannel.Email,
            ReceivedDaysAgo: 160,
            ResponseDays: 12,
            ["WOŚ"],
            "W {Y-1} r. w ramach miejskiego programu ograniczania niskiej emisji złożono 486 wniosków o dofinansowanie wymiany źródła ciepła, z których 431 rozpatrzono pozytywnie.\n\n"
            + "Łączna kwota wypłaconych dotacji wyniosła 3 862 000 zł.\n\n"
            + "Dzięki programowi zlikwidowano 517 pieców i kotłów na paliwo stałe; najczęściej zastępowano je podłączeniem do sieci ciepłowniczej (52%) oraz pompami ciepła (31%)."),
        new(
            "Koszt organizacji obchodów Dni Bydgoszczy w {Y-1} r.",
            "Proszę o udostępnienie informacji o całkowitym koszcie organizacji obchodów Dni Bydgoszczy w {Y-1} r., w tym koncertów plenerowych. "
            + "Proszę o wskazanie kwot przeznaczonych na wynagrodzenia artystów, obsługę techniczną i ochronę, a także informację o wpływach od sponsorów.",
            "Tomasz Wróblewski",
            "t.wroblewski@example.com",
            ReplyChannel.Email,
            ReceivedDaysAgo: 130,
            ResponseDays: 11,
            ["BKB", "BP"],
            "Całkowity koszt organizacji obchodów Dni Bydgoszczy w {Y-1} r. wyniósł 1 284 600 zł brutto, w tym:\n"
            + "– wynagrodzenia artystów – 586 000 zł,\n"
            + "– obsługa techniczna (scena, nagłośnienie, oświetlenie, telebimy) – 412 300 zł,\n"
            + "– ochrona i zabezpieczenie medyczne – 168 900 zł,\n"
            + "– promocja – 117 400 zł.\n\n"
            + "Wpływy od sponsorów wyniosły 210 000 zł."),
        new(
            "Nasadzenia zastępcze drzew w {Y-1} r.",
            "W związku z licznymi wycinkami drzew w mieście proszę o informację, ile drzew posadzono w Bydgoszczy w {Y-1} r. w ramach nasadzeń zastępczych nakładanych w decyzjach zezwalających na usunięcie drzew. "
            + "Proszę także o podanie łącznego kosztu nasadzeń realizowanych przez Miasto oraz informację, w jaki sposób monitorowana jest ich przeżywalność.",
            "Katarzyna Lewandowska",
            "k.lewandowska@example.com",
            ReplyChannel.Email,
            ReceivedDaysAgo: 60,
            ResponseDays: 12,
            ["WOŚ", "WGK"],
            "W {Y-1} r. w ramach nasadzeń zastępczych posadzono na terenach miejskich 1 342 drzewa. Koszt nasadzeń realizowanych przez Miasto wyniósł 2 418 000 zł brutto (średnio ok. 1 800 zł za drzewo wraz z trzyletnią pielęgnacją).\n\n"
            + "Przeżywalność nasadzeń jest kontrolowana w okresie gwarancyjnym przez inspektorów nadzoru. Drzewa, które się nie przyjęły, wykonawca ma obowiązek wymienić na własny koszt – w {Y-1} r. wymieniono w ten sposób 87 drzew."),
        new(
            "Zakupy sprzętu komputerowego dla Urzędu Miasta",
            "Proszę o informację, ile komputerów, laptopów i monitorów zakupił Urząd Miasta Bydgoszczy w {Y-1} r. oraz jaka była łączna wartość tych zakupów. "
            + "Proszę również o informację, w jakim trybie dokonano zakupów i co dzieje się z wycofywanym sprzętem.",
            "Paweł Szymański (redakcja „Kurier Fordoński”)",
            "redakcja@kurier-fordonski.example.org",
            ReplyChannel.Email,
            ReceivedDaysAgo: 35,
            ResponseDays: 8,
            ["WI", "WZP"],
            "W {Y-1} r. Urząd Miasta Bydgoszczy zakupił 214 komputerów stacjonarnych, 126 laptopów i 290 monitorów za łączną kwotę 1 486 200 zł brutto.\n\n"
            + "Zakupy zrealizowano w trybie przetargu nieograniczonego oraz z wykorzystaniem centralnego zamawiającego.\n\n"
            + "Sprawny sprzęt wycofywany z użytku jest w pierwszej kolejności przekazywany miejskim jednostkom organizacyjnym i szkołom."),
        new(
            "Koszt sylwestrowego koncertu i pokazu świateł",
            "Uprzejmie proszę o informację, jaką kwotę Miasto wydało na przygotowanie koncertu sylwestrowego i pokazu świateł w ostatni dzień {Y-1} r. "
            + "Proszę o rozbicie kosztów na poszczególne pozycje oraz o podanie, ile osób uczestniczyło w wydarzeniu według szacunków organizatora.",
            "Rafał Nowicki",
            "rafal.nowicki@example.com",
            ReplyChannel.Email,
            ReceivedDaysAgo: 285,
            ResponseDays: 10,
            ["BKB"],
            "Koszt przygotowania koncertu sylwestrowego i pokazu świateł wyniósł łącznie 612 400,00 zł brutto. Największe pozycje stanowiły honoraria artystów (268 000,00 zł) oraz scena, nagłośnienie i oświetlenie (154 900,00 zł); pokaz multimedialny i pirotechniczny kosztował 98 500,00 zł.\n\n"
            + "Według szacunków organizatora w wydarzeniu uczestniczyło około 25 tysięcy osób."),
    ];
}
