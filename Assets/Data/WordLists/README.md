# Word lists

## `google-10000-english-usa-no-swears.txt`

- **Source:** [first20hours/google-10000-english](https://github.com/first20hours/google-10000-english)
- **Contents:** 9,884 English words, ordered by descending frequency in the Google Web Trillion Word
  Corpus. Line number == frequency rank, which `WordBankSO` uses directly as its difficulty proxy.
- **License:** see `LICENSE.google-10000-english.md` in this folder.

### ⚠️ Commercial-use caveat

The upstream licence permits *"educational and personal/research use"* and explicitly says:

> I do not recommend using this data for commercial purposes without licensing it from the
> Linguistic Data Consortium.

This list is therefore fine for development and playtesting, but **must be replaced or licensed
before shipping a commercial build**. Permissively licensed alternatives:

| List | Licence | Note |
|---|---|---|
| [dwyl/english-words](https://github.com/dwyl/english-words) | Unlicense (public domain) | ~479k words, but **unranked** — no frequency signal, so tiers must be derived from length alone |
| [SCOWL / word lists](http://wordlist.aspell.net/) | MIT-like, per-file | Curated, size-graded (`en_US-large` etc.) |
| [wordfreq](https://github.com/rspeer/wordfreq) | Apache-2.0 (code), data varies | Frequency data; export a static list offline |

Swapping is a one-field change: drop the new `.txt` (one word per line, most-frequent first) into
this folder and assign it to `WordBankSO.sourceList` on `Assets/Data/WordBank.asset`. Nothing in
the gameplay code reads this file directly.

If you pick an **unranked** list, also set `WordBankSO.rankingMode` to `LengthOnly` so tiering falls
back to word length instead of line order.
