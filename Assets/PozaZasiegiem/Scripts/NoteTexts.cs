using System.Collections.Generic;

/// <summary>
/// Treść notatek fabularnych – jedno źródło dla generatora poziomu i dla scen już zbudowanych.
/// Notatki piszą ludzie, którzy próbują tu przetrwać, więc są IN CHARACTER:
/// żadnych klawiszy ani pojęć z gry. Sterowanie pokazuje HUD i menu (Sterowanie).
/// </summary>
public static class NoteTexts
{
    public struct Entry
    {
        public string title, text, titleEn, textEn;
        public Entry(string title, string text, string titleEn, string textEn)
        { this.title = title; this.text = text; this.titleEn = titleEn; this.textEn = textEn; }
    }

    public const string Start = "start", Kitchen = "kitchen", Freezer = "freezer", Dining = "dining", Office = "office";

    public static readonly Dictionary<string, Entry> All = new Dictionary<string, Entry>
    {
        [Start] = new Entry(
            "Pognieciona kartka",
            "Dzień 34. Chyba. Tu nie ma nocy, więc liczę drzemki.\n\n" +
            "Jeśli ktoś to czyta – NIE BIEGAJ. To coś słyszy kroki.\n" +
            "Idź nisko, na ugiętych nogach. Wtedy prawie cię nie słychać.\n" +
            "Latarka pomaga, ale widać ją z daleka. I nigdy nie świeć mu prosto w twarz.\n\n" +
            "Metalowe szafki ratują życie. Kiedy zacznie węszyć przy twojej – nie oddychaj.\n" +
            "Ale jeśli zobaczy, jak do niej wchodzisz...\n\n" +
            "Pod sufitem biegną grube rury. Ono w nich chodzi – słychać stukanie, a rury drżą.\n" +
            "Jeśli masz kamerę, przełącz ją na tryb nocny. Widać w nim po ciemku, a tego światła ono nie widzi.\n\n" +
            "Zostawiłem wykrywacz ruchu niedaleko stąd. W biurze stoi stara radiostacja – jeszcze działa.\n" +
            "Telefony tu milczą. Tylko ona może nas stąd wyciągnąć.\n\n– K.",
            "Crumpled note",
            "Day 34. I think. There's no night here, so I count naps.\n\n" +
            "If anyone reads this – DON'T RUN. That thing hears footsteps.\n" +
            "Stay low, knees bent. Then it can barely hear you.\n" +
            "A flashlight helps, but it can be seen from far away. And never shine it in its face.\n\n" +
            "The metal lockers save lives. If it starts sniffing at yours – don't breathe.\n" +
            "But if it sees you getting in...\n\n" +
            "Thick pipes run under the ceiling. It moves inside them – you hear knocking, and the pipes shake.\n" +
            "If you have a camera, switch it to night mode. You can see in the dark with it, and it can't see that light.\n\n" +
            "I left a motion tracker not far from here. There's an old radio in the office – it still works.\n" +
            "Phones are dead down here. Only the radio can get us out.\n\n– K."),

        [Kitchen] = new Entry(
            "Lista obecności",
            "KTO JESZCZE JEST\n\n" +
            "Dzień 12: Kasia, Tomek, Marek, Ola\n" +
            "Dzień 15: Kasia, Tomek, Ola\n" +
            "Dzień 19: Kasia, Tomek, Pracownik\n" +
            "Dzień 22: Kasia, Pracownik\n" +
            "Dzień 26: Pracownik\n\n" +
            "(dopisane innym charakterem pisma)\n" +
            "Kto dopisuje „Pracownika”? Nikogo takiego z nami nie było.\n\n" +
            "Piece rosną. Wczoraj sięgały mi do pasa. Przy grillu wisi gaśnica – weź ją.",
            "Roll call",
            "WHO'S STILL HERE\n\n" +
            "Day 12: Kate, Tom, Mark, Ola\n" +
            "Day 15: Kate, Tom, Ola\n" +
            "Day 19: Kate, Tom, Employee\n" +
            "Day 22: Kate, Employee\n" +
            "Day 26: Employee\n\n" +
            "(added in different handwriting)\n" +
            "Who keeps writing \"Employee\"? Nobody like that was ever with us.\n\n" +
            "The ovens are growing. Yesterday they came up to my waist. There's an extinguisher by the grill – take it."),

        [Freezer] = new Entry(
            "Kartka przyklejona do drzwi",
            "Do każdego, kto tu trafi:\n\n" +
            "Coś chodzi w rurach pod sufitem. Najpierw słychać stukanie,\n" +
            "potem rury drżą i z połączeń sypie się kurz. Kiedy leci kurz – STÓJ. Nie ruszaj się.\n\n" +
            "Czasem czeka nad włazem. Słychać wtedy ciche mruczenie, a z rury kapie.\n" +
            "NIE PRZECHODŹ POD SPODEM.\n\n" +
            "Gaśnica je odstrasza. Psiknąłem mu prosto w pysk – uciekło do rury.\n" +
            "Ale wraca. Zawsze wraca tam, gdzie ostatnio słyszało ludzi.\n\n" +
            "– Marek. Idę szukać wyjścia. Nie czekajcie na mnie.",
            "Note taped to the door",
            "To anyone who ends up here:\n\n" +
            "Something walks in the pipes under the ceiling. First you hear knocking,\n" +
            "then the pipes shake and dust falls from the joints. When the dust falls – STOP. Don't move.\n\n" +
            "Sometimes it waits above a hatch. You hear a quiet purring, and the pipe drips.\n" +
            "DON'T WALK UNDERNEATH.\n\n" +
            "The extinguisher scares it off. I sprayed it right in the face – it fled into a pipe.\n" +
            "But it comes back. It always comes back to where it last heard people.\n\n" +
            "– Mark. Going to look for a way out. Don't wait for me."),

        [Dining] = new Entry(
            "Wyrwana kartka z pamiętnika",
            "Nie umiem znaleźć wyjścia. Te same krzesła, te same stoły, te same lampy.\n" +
            "Wszystko jest trochę za duże. Albo to ja się kurczę.\n\n" +
            "Wczoraj stoły wisiały pod sufitem. Dziś stoją normalnie i udają, że nic się nie stało.\n\n" +
            "Na końcu sali ktoś stoi pod ścianą. W czapce, w uniformie. Od tygodnia się nie rusza.\n" +
            "Ale kiedy się odwracam – stoi bliżej.\n\n" +
            "Przestałam się odwracać.\n\n– Ola",
            "Torn diary page",
            "I can't find the exit. The same chairs, the same tables, the same lamps.\n" +
            "Everything is a little too big. Or I'm shrinking.\n\n" +
            "Yesterday the tables hung from the ceiling. Today they stand normally and pretend nothing happened.\n\n" +
            "At the far end of the hall someone stands by the wall. In a cap, in a uniform. Hasn't moved in a week.\n" +
            "But when I turn around – he's standing closer.\n\n" +
            "I stopped turning around.\n\n– Ola"),

        [Office] = new Entry(
            "Transkrypcja – 108.7 MHz",
            "TRANSKRYPCJA – 108.7 MHz (fragment)\n\n\"...jeśli ktoś to słyszy, tu Agencja ECHO.\n" +
            "Ta strefa jest POZA ZASIĘGIEM naszych sieci. Jedyny kanał to radiostacja w biurze.\n" +
            "Potrzebne: antena, lampa nadawcza, zasilacz.\n" +
            "Nadaj sygnał, a przyjdziemy. Nie ufaj niczemu, co nosi uniform...\"\n\n[szum]\n\n" +
            "(dopisane ołówkiem)\nSłuchałam tego sto razy. Oni naprawdę przyjdą. Muszą.",
            "Transcript – 108.7 MHz",
            "TRANSCRIPT – 108.7 MHz (fragment)\n\n\"...if anyone can hear this, this is the ECHO Agency.\n" +
            "This zone is OUT OF RANGE of our networks. The only channel is the radio in the office.\n" +
            "Required: antenna, transmitter tube, power supply.\n" +
            "Send the signal and we will come. Don't trust anything that wears a uniform...\"\n\n[static]\n\n" +
            "(added in pencil)\nI've listened to this a hundred times. They will come. They have to."),
    };

    /// <summary>Tytuły notatek ze scen zbudowanych przed wprowadzeniem id – pozwala podmienić treść bez przebudowy mapy.</summary>
    static readonly Dictionary<string, string> LegacyTitles = new Dictionary<string, string>
    {
        ["Pognieciona kartka"] = Start,
        ["Grafik zmian"] = Kitchen,
        ["Notatka kierownika zmiany"] = Freezer,
        ["Ankieta klienta"] = Dining,
        ["Transkrypcja – 108.7 MHz"] = Office,
    };

    public static bool TryGet(string id, string legacyTitle, out Entry e)
    {
        if (!string.IsNullOrEmpty(id) && All.TryGetValue(id, out e)) return true;
        if (!string.IsNullOrEmpty(legacyTitle) && LegacyTitles.TryGetValue(legacyTitle, out var key)) return All.TryGetValue(key, out e);
        e = default;
        return false;
    }
}
