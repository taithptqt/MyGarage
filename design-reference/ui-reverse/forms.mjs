// Publicly rendered Storage fields, captured from the source UI on 2026-11-13.
// No upstream credentials or submissions are needed by the local preview.
const field = (id, label, type = 'text', required = false) => ({
  id, formId: 8, label, type, isRequired: required, visibility: 'visible', defaultValue: ''
});
const extras = [
  [14, 'Stilstandsforsikring: Kontakt for tilbud'],
  [15, 'Montering af lader 195 kr.'],
  [4, 'Klargøring Deluxe 2.395 kr.'],
  [18, 'Kontrol af dæktryk & påfyldning af max dæktryk 395 kr.'],
  [17, 'Kontrol af olie, sprinkler- og kølervæske (ekskl. påfyldning) 395 kr.'],
  [19, 'Kontrol af batteri samt montering af lader (inkl. vedligeholdelseslader) 995 kr.'],
  [23, 'Syn af bil inkl. kørsel 895 kr.'], [21, 'Toldsyn af bil inkl. kørsel 1.595 kr.'],
  [30, 'Afhentning / levering af bil i lukket trailer pr. time + Yderligere udgifter 750 kr.'],
  [24, 'Afmontering og afmelding af nummerplader 395 kr.'],
  [28, 'Indregistrering inkl. nummerplader og montering 1.895 kr.'],
  [29, 'Tankning af brændstof + Dagspris pr. liter 195 kr.'],
  [31, 'Indleveringstjek: Påfyldning af max dæktryk, montering af vedligeholdelseslader, afmontering og afmelding af nummerplade 1.995 kr.'],
  [25, 'Udleveringstjek: Kontrol af dæktryk, dækmønster, væsker, evt. opfyldning, afmontering af lader 1.995 kr.']
];
export const storageForm = {
  id: 8, title: 'Bilopbevaring', button: { text: 'Send forespørgsel', type: 'text' },
  fields: [field(1, 'Navn'), field(3, 'Mail', 'email', true), field(2, 'Telefon', 'text', true),
    field(32, 'Bilens mærke', 'text', true), field(33, 'Model', 'text', true), field(34, 'Årgang', 'text', true),
    ...extras.map(([id, label]) => ({ ...field(id, 'Tilkøb af ekstra ydelser', 'checkbox'), inputs: [{ id: `${id}.1`, label }] })),
    { ...field(35, '', 'hidden'), visibility: 'hidden' }]
};
const contact = (id, fields, text = 'Send') => ({id, button:{text}, fields:fields.map(([fieldId,label,type='text',isRequired=false,visibility='visible'])=>({id:fieldId,formId:id,label,type,isRequired,visibility,defaultValue:''}))});
const callback = (id, text = 'Bliv kontaktet') => contact(id, [[1,'Navn','text',true],[4,'E-mail','email',true],[3,'Telefonnummer','text',true]], text);
const choices = values => values.map(text=>({text,value:text}));
export const localForms = {
  8: storageForm,
  4: contact(4, [[1,'Navn','text',true],[2,'E-mail','email',true],[3,'Besked','textarea']]),
  5: contact(5, [[1,'Navn','text',true],[2,'E-mail','email',true],[3,'Besked','textarea'],[5,'Partner email','email',false,'hidden']]),
  7: contact(7, [[1,'Name'],[2,'E-mail','email'],[3,'Message','textarea']]),
  13: contact(13, [[1,'Navn','text',true],[3,'Telefonnummer','text',true],[4,'E-mail','email',true],[8,'Vælg afdeling','select',true],[6,'Send besked','textarea']]),
  14: callback(14), 20: callback(20), 21: callback(21,'Book prøvekørsel'),
  22: callback(22,'Book prøvekørsel'), 23: callback(23,'Bliv kontaktet af vores salgsafdeling'),
  28: callback(28,'Bliv kontaktet af vores servicecenter'),
  24: contact(24, [[8,'Navn','text',true],[9,'Telefon','text',true],[4,'E-mail','email',true],[5,'Vælg afdeling','select'],[6,'Jeg vil gerne høre mere om','select']])
};
localForms[13].fields.find(f=>f.id===8).choices = choices(['Vejle','København','Aalborg']);
localForms[24].fields.find(f=>f.id===5).choices = choices(['Vejle','København','Aalborg']);
localForms[24].fields.find(f=>f.id===6).choices = choices(['Eletre','Emeya','Emira','Evija']);
