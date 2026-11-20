// Classification, origin and holding for the seed records (fields added by the change extend-catalogue-fields).
// Kept apart from data.mjs so the original records stay readable. All holders, places and values are fictional.
//
// Deliberate edge cases:
// - COL-2022-033 has no entry here: the "No object type" / "No collection group" groups are never empty.
// - COL-2006-015 has an uncertain place of production kept as written and not linked to a region.
// - COL-2008-090 was bought in Italian lire (ITL), a withdrawn currency; COL-1964-201 is valued in ITL.
// - Several records share a collection group written in different case ("OLANDA" / "Olanda"), which must group together.
// - Exhibitions in a given order on a few records; long English catalogue entries with line breaks on two.

/** Object types with their English and Italian names; records refer to them by the English name. */
export const objectTypes = [
  { en: 'Paintings', it: 'Dipinti' },
  { en: 'Drawings and prints', it: 'Disegni e stampe' },
  { en: 'Ceramics', it: 'Ceramiche' },
  { en: 'Textiles', it: 'Tessuti' },
  { en: 'Carpets', it: 'Tappeti' },
  { en: 'Metalwork', it: 'Metalli' },
  { en: 'Coins and medals', it: 'Monete e medaglie' },
  { en: 'Lacquer', it: 'Lacche' },
  { en: 'Miniatures', it: 'Miniature' },
  { en: 'Sculpture', it: 'Scultura' },
]

/** Italian names of the regions in data.mjs (keyed by their English name). A few are left English-only on purpose. */
export const regionNamesIt = {
  Europe: 'Europa', 'Low Countries': 'Paesi Bassi', Flanders: 'Fiandre', Holland: 'Olanda', Amsterdam: 'Amsterdam', Antwerp: 'Anversa',
  France: 'Francia', Paris: 'Parigi', Lyon: 'Lione', Italy: 'Italia', Venice: 'Venezia', Florence: 'Firenze', Naples: 'Napoli',
  England: 'Inghilterra', London: 'Londra', 'German lands': 'Terre tedesche', Saxony: 'Sassonia', Switzerland: 'Svizzera', Geneva: 'Ginevra',
  'East Asia': 'Asia orientale', China: 'Cina', Jingdezhen: 'Jingdezhen', Japan: 'Giappone', Kyoto: 'Kyoto', 'Ottoman Empire': 'Impero ottomano',
  Istanbul: 'Istanbul', Persia: 'Persia', Isfahan: 'Isfahan',
}

/**
 * Descriptions per language. data.mjs holds the English descriptions; these add Italian ones. Records listed in
 * `italianOnly` keep only the Italian text, so readers in English see the fallback marked as Italian.
 */
export const descriptionsIt = {
  'COL-1998-014': 'Una tavola apparecchiata con un limone parzialmente sbucciato, un roemer e un piatto di peltro. La buccia pende oltre il bordo del tavolo secondo il gusto dell’epoca.',
  'COL-1987-112': 'Un grande piatto con una fenice al centro tra peonie e una tesa a otto riserve. La lunga provenienza documenta un percorso attraverso collezioni europee.',
  'COL-1971-130': 'Tappeto da preghiera con mihrab e colonne, in vello di lana su fondazione di lana.',
  'COL-2006-015': 'Brocca in forma di gallo, in fritta con invetriatura turchese.',
  'COL-2015-044': 'Frammento di kosode con glicine, seta tinta a riserva e ricamata.',
}
export const italianOnly = ['COL-2006-015', 'COL-2015-044']

/** Keyed by inventory number. `place.region` names a region from data.mjs; `place.text` is kept as written. */
export const catalogue = {
  'COL-1998-014': {
    type: 'Paintings', group: 'FIANDRE', place: { text: 'Anversa', region: 'Antwerp' }, holder: 'Fondazione Verhaghe', location: 'MI F3',
    exhibitions: ['Natura morta fiamminga, Palazzo Reale, Milano, 2004', 'Still Lifes of the Golden Age, Mauritshuis, The Hague, 1998'],
    valuation: { amount: 95000, currency: 'EUR' },
  },
  'COL-2011-007': { type: 'Drawings and prints', group: 'FRANCIA', location: 'GE cassettiera 2' },
  'COL-1987-112': {
    type: 'Ceramics', group: 'CINA', place: { text: 'Jingdezhen', region: 'Jingdezhen' }, dynasty: 'Qing (Kangxi)', location: 'MI 16',
    lot: '212',
    entry: 'A LARGE BLUE AND WHITE DISH\nQING DYNASTY, KANGXI PERIOD (1662-1722)\nPainted in the well with a phoenix among peony scrolls,\nthe border with eight panels of flowering branches.',
    exhibitions: ['Blue and White, Victoria and Albert Museum, London, 1985'],
  },
  'COL-2003-031': { type: 'Paintings', group: 'OLANDA', place: { text: 'Olanda', region: 'Holland' }, location: 'MI F3' },
  'COL-1964-201': {
    type: 'Ceramics', group: 'PERSIA', place: { text: 'Isfahan', region: 'Isfahan' }, dynasty: 'Safavide', location: 'cella 16',
    valuation: { amount: 350000000, currency: 'ITL' },
  },
  'COL-2015-044': { type: 'Textiles', group: 'GIAPPONE', place: { text: 'Kyoto', region: 'Kyoto' }, dynasty: 'Edo', location: 'GE armadio 4' },
  'COL-1952-003': { type: 'Coins and medals', group: 'ITALIA', place: { text: 'Venezia', region: 'Venice' }, location: 'MI cassaforte' },
  'COL-1952-004': { type: 'Coins and medals', group: 'ITALIA', place: { text: 'Firenze', region: 'Florence' }, dynasty: 'Medici', location: 'MI cassaforte' },
  'COL-2019-018': { type: 'Metalwork', group: 'SVIZZERA', place: { text: 'Ginevra', region: 'Geneva' }, location: 'MI cassaforte', lot: '48' },
  'COL-1979-066': { type: 'Ceramics', group: 'GERMANIA', place: { text: 'Meissen', region: 'Saxony' }, location: 'MI 16' },
  'COL-2008-090': {
    type: 'Drawings and prints', group: 'ITALIA', place: { text: 'Venezia', region: 'Venice' }, location: 'GE cassettiera 1',
    price: { amount: 18500000, currency: 'ITL' }, note: 'Pagato in due rate',
  },
  'COL-1971-130': {
    type: 'Carpets', group: 'ANATOLIA', place: { text: 'Anatolia occidentale?', region: 'Istanbul' }, dynasty: 'Ottomana', holder: 'Rossetti Fine Arts srl', location: 'Beta2',
    exhibitions: ['Arte Ottomana 1450-1600, Palazzo Lomellino, Genova, 1999', 'Tappeti d’Oriente, Palazzo Ducale, Genova, 2003'],
  },
  'COL-1971-131': { type: 'Textiles', group: 'LAMPASSI', place: { text: 'Istanbul o Bursa', region: 'Istanbul' }, dynasty: 'Ottomana', location: 'Beta2' },
  'COL-2021-002': { type: 'Ceramics', group: 'GIAPPONE', place: { text: 'Kyoto', region: 'Kyoto' }, location: 'MI 16' },
  'COL-1990-057': { type: 'Paintings', group: 'Olanda', place: { text: 'Haarlem?', region: 'Holland' }, location: 'MI F3' },
  'COL-1990-058': { type: 'Paintings', group: 'olanda ', place: { text: 'Olanda', region: 'Holland' }, location: 'MI F3' },
  'COL-2001-012': { type: 'Drawings and prints', group: 'FRANCIA', place: { text: 'Parigi', region: 'Paris' }, location: 'GE cassettiera 2' },
  'COL-2001-013': { type: 'Drawings and prints', group: 'FRANCIA', place: { text: 'Parigi', region: 'Paris' }, location: 'GE cassettiera 2' },
  'COL-1958-009': { type: 'Ceramics', group: 'CINA', place: { text: 'Jingdezhen', region: 'Jingdezhen' }, dynasty: 'Qing (Kangxi)', location: 'MI 16' },
  'COL-2017-120': { type: 'Lacquer', group: 'GIAPPONE', place: { text: 'Edo (Tokyo)', region: 'Edo' }, dynasty: 'Edo', location: 'GE armadio 4', lot: '305' },
  'COL-1993-005': { type: 'Metalwork', group: 'INGHILTERRA', place: { text: 'Londra', region: 'London' }, dynasty: 'Giorgiana', location: 'MI cassaforte' },
  'COL-1985-077': { type: 'Miniatures', group: 'INGHILTERRA', place: { text: 'Londra', region: 'London' }, location: 'MI cassaforte' },
  'COL-2010-061': { type: 'Textiles', group: 'INGHILTERRA', place: { text: 'Inghilterra', region: 'England' }, dynasty: 'Stuart', location: 'GE armadio 3' },
  'COL-1966-140': { type: 'Coins and medals', group: 'OLANDA', place: { text: 'Olanda', region: 'Holland' }, location: 'MI cassaforte' },
  'COL-2024-001': { type: 'Ceramics', group: 'OLANDA', place: { text: 'Delft', region: 'Holland' }, location: 'MI 16' },
  'COL-1949-022': {
    type: 'Paintings', group: 'OLANDA', place: { text: 'Leida', region: 'Holland' }, holder: 'Rossetti Fine Arts srl', location: 'MI F3',
    entry: 'A LADY AT HER TOILETTE\nLEIDEN, LATE 17TH CENTURY\nOil on panel. The mirror held by the maid reflects a window\nthat is not otherwise shown in the room.',
  },
  'COL-2006-015': { type: 'Ceramics', group: 'PERSIA', place: { text: 'Iran o Siria?' }, dynasty: 'Selgiuchide', location: 'cella 16' },
  'COL-2013-099': { type: 'Textiles', group: 'INGHILTERRA', place: { text: 'Inghilterra', region: 'England' }, location: 'GE armadio 3' },
  'COL-1975-040': { type: 'Sculpture', location: 'magazzino' },
}
