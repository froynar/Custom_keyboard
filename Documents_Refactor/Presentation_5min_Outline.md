# Custom Keyboard Builder — 5-Minute Presentation Outline

**Course:** Technical Writing and Presentation
**Format:** Spoken talk, ~5 minutes, English
**Goal (Zeroth Law — *have a purpose*):** Make the audience understand *one* idea —
> *"We turned a confusing custom-keyboard buying process into a simple, guided builder that checks every choice for you."*

Everything in the talk serves this single message. If a slide does not push this idea from your head into the audience's head, cut it.

---

## How this outline uses the two readings

This talk is built directly on the two PDFs, so you can point to each principle if asked.

**Doumont — Three Laws of Professional Communication**

- **Zeroth Law – Have a purpose:** one key message, stated above and repeated at the end.
- **First Law – Adapt to your audience:** classmates + instructor, *not* keyboard hobbyists. So we explain the *problem* in everyday terms before any database words.
- **Second Law – Maximize the signal-to-noise ratio:** few words per slide, one diagram per idea, no jargon dumps, no "we built tables X, Y, Z…" lists.
- **Third Law – Use effective redundancy:** say the key message out loud, show it on the slide, and reinforce it with one running example (a 65% mechanical build) — same idea, different channels.

**Simon Peyton Jones — How to write a great research paper / talk**

- **Narrative spine:** *Here is a problem → it's a real problem → here is our idea → our idea works → here's how it compares.*
- **Convey the idea first, details second.** The audience gets the intuition; the schema/validation are evidence, shown briefly.
- **Use examples right away** (the running build) instead of starting from the abstract data model.
- **State contributions explicitly** — don't make the audience guess what you actually did.
- **Don't be intimidated:** a class project is a perfectly good "idea" to present.

---

## Time budget (≈5:00)

| # | Slide | Time | Role in the narrative |
| - | --- | ---: | --- |
| 1 | Title | 0:10 | Set up |
| 2 | The problem | 0:50 | "Here is an interesting problem" |
| 3 | Our idea + contributions | 1:00 | "Here is my idea" (explicit) |
| 4 | How it works (3 roles + example) | 1:20 | Convey the idea via example |
| 5 | Under the hood (evidence) | 1:00 | "My idea works" (details as proof) |
| 6 | What refactoring taught us | 0:40 | Comparison / reflection |
| 7 | Takeaway | 0:30 | Repeat the purpose |
| | **Total** | **≈5:10** | (buffer for transitions) |

---

## Slide-by-slide

### Slide 1 — Title (0:10)

**On screen (signal only):**
- Custom Keyboard Builder
- One line: *"From a pile of parts to a working keyboard — guided and validated."*
- Your name · Course · Date

**Say:**
> "Hi, I'm [name]. In the next five minutes I'll show how we took a confusing process — building a custom keyboard — and turned it into a simple guided app."

*Principle:* Zeroth Law — open by naming the purpose, not the tech stack.

---

### Slide 2 — The problem (0:50)

**On screen:**
- A photo/sketch of scattered keyboard parts (kit, switches, keycaps, stabilizers)
- Three short pains: *Too many parts. Will they fit? Did I overpay?*

**Say:**
> "Building a custom keyboard means choosing a kit, switches, keycaps, stabilizers, accessories — dozens of combinations. Beginners face three problems: there are too many parts, it's hard to know what's *compatible*, and it's easy to lose track of the *price*. Most shops just show you a catalog and leave you to figure it out."

*Principle:* First Law — frame the problem in human terms (fit, money, overwhelm), no database vocabulary yet. SPJ — make the problem interesting and concrete before the solution.

---

### Slide 3 — Our idea + contributions (1:00)

**On screen:**
- Big: *A guided builder that validates every choice.*
- Bulleted **contributions** (explicit, refutable):
  1. Start from a **keyboard kit**, then add only the parts you need.
  2. **Automatic validation** of compatibility, quantity, and price.
  3. **Send the build to a seller** to be assembled — with a price snapshot.

**Say:**
> "Our idea: instead of a flat catalog, start from one *kit* — which already includes the case, PCB and plate — and add only switches, keycaps and stabilizers on top. As you build, the app checks compatibility and the required number of switches, and it keeps a running total. When you're happy, you send the build to a seller to assemble. So our three contributions are: a kit-based builder, automatic validation, and a request workflow with a locked-in price."

*Principle:* SPJ — *state your contributions as an explicit list*; the rest of the talk substantiates them. Second Law — three items, not ten.

---

### Slide 4 — How it works: three roles + a real example (1:20)

**On screen:**
- Simple flow: **Buyer → Build → Request → Seller**, with **Admin** managing the catalog, and a small **Chat** icon between buyer and seller.
- A running example callout: *Example: a 65% mechanical build — kit + 70 switches + keycap set + stabilizers = $225.30.*

**Say:**
> "There are three roles. The **Buyer** picks a kit and configures a build — here's a real example from our data: a 65% mechanical board, a kit plus 70 switches, a keycap set and stabilizers, totaling about 225 dollars. The app won't let them request it until the switch count matches the kit and every part is compatible. The **Seller** receives that request and updates its status as they build it. The **Admin** manages the catalog and approves sellers. Buyer and seller can chat inside a request if they need to clarify anything."

*Principle:* SPJ — *use an example right away*; the audience follows the intuition. Third Law — the same example reappears on the next slide (redundancy across slides).

---

### Slide 5 — Under the hood (evidence that it works) (1:00)

**On screen:**
- One clean diagram: `builds` → `build_items` (switch / keycap / stabilizer / accessory), with a "validation" badge.
- Three evidence points: *18-table data model · automatic price = kit + items · status flow Pending → … → Completed.*

**Say:**
> "Briefly, how do we *know* it works? A build is stored as a kit plus a list of items, each with its own quantity and a price snapshot — so the total is always the kit price plus the items, checked automatically. The same example I showed adds up to exactly 225.30. The whole thing runs on a focused data model of 18 tables, and seller requests move through a clear status flow. The details matter, but the point is: every rule the buyer relies on is enforced by the system, not by hope."

*Principle:* SPJ — *provide details, but as evidence for the claims*, after the idea is clear. First Law — keep it to the *one* diagram the audience can actually read in a talk.

---

### Slide 6 — What refactoring taught us (0:40)

**On screen:**
- Before vs after: *Old: separate tables for case / PCB / plate, per-key compatibility, inventory…* → *New: kit-based, 18 tables, simpler rules.*
- One line: *Less is more — we removed features to make it clearer.*

**Say:**
> "One thing I'm proud of is what we *removed*. The first version tried to model every screw — separate case, PCB and plate tables, per-key compatibility, seller inventory. We refactored to a 'realistic kit-shop' model where the kit bundles the basics. Fewer tables, fewer rules, and the project actually became buildable. That's the same lesson as today's readings: cut the noise so the signal comes through."

*Principle:* SPJ — *comparison to the alternative*; Second Law made tangible (removing noise from a *system*, not just a slide). This ties your project back to the course theme.

---

### Slide 7 — Takeaway (0:30)

**On screen:**
- Repeat the one message: *"A guided, validated builder beats a pile of parts."*
- *Thank you — questions?*

**Say:**
> "So if you remember one thing: we turned a confusing pile of parts into a guided builder that checks compatibility, quantity and price for you, and hands a clean, priced request to the seller. Thanks — I'm happy to take questions."

*Principle:* Zeroth Law + Third Law — end by repeating the purpose you opened with.

---

## Delivery tips (from the readings)

- **Active voice, simple words** (SPJ): say "the app checks the price," not "the price is validated by the system."
- **One idea per slide; minimal text** (Second Law): slides are cues, *you* carry the content with your voice.
- **Don't read the slide** — the slide and your speech are two channels (Third Law); use them to reinforce, not duplicate word-for-word.
- **Rehearse with a timer.** 5 minutes is short; the example on slides 4–5 is your safety anchor if you start to ramble.
- **Vary tone by content, not for show** (Doumont's closing point): slow down on the one diagram, speed up on transitions.

## Optional Q&A backups (don't put on slides)

- *Tech stack:* WPF / C# desktop app (MVVM), SQL Server database.
- *Why "build_items" instead of columns on the build?* Each item has its own quantity and price snapshot, so the build table stays small and the total is easy to compute.
- *Why a price snapshot?* So the agreed price doesn't change if the catalog price changes later.
