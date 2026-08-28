using System.Reflection;
using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

public partial class Main : Control
{
	public LocalGameDialog LocalGameDialog;
	public NewLocalGameMenu NewLocalGameMenu;
	public NewPlayerMenu NewPlayerMenu;
	public Kontor Kontor;
	public AbrechnungDialog AbrechnungDialog;
	public Stadt Stadt;
	public LoadGameDialog LoadGameDialog;
	public SaveGameDialog SaveGameDialog;
	public Schreibstube Schreibstube;
	public KreditbuchDialog KreditbuchDialog;
	public GesetzeDialog GesetzeDialog;
	public HofhaltungDialog HofhaltungDialog;
	public Weltkarte Weltkarte;
	public HausVerwaltungDialog HausVerwaltungDialog;
	public HausWaehlenDialog HausWaehlenDialog;
	public HausErweiterungenDialog HausErweiterungenDialog;
	public BewerbDialog BewerbDialog;
	public BewerbInfosDialog BewerbInfosDialog;
	public WahlDialog WahlDialog;
	public Kirche Kirche;
	public TestamentDialog TestamentDialog;
	public BrautwerbungDialog BrautwerbungDialog;
	public GeburtDialog GeburtDialog;
	public Kirchgang Kirchgang;
	public KonfessionslosDialog KonfessionslosDialog;
	public PrivilegienDialog PrivilegienDialog;
	public FestGebenDialog FestGebenDialog;
	public BauwerkStiftenDialog BauwerkStiftenDialog;
	public ProzentwertFestlegenDialog ProzentwertFestlegenDialog;
	public UntergebeneDialog UntergebeneDialog;
	public RohstoffpreiseDialog RohstoffpreiseDialog;
	public KontrahentenDialog KontrahentenDialog;
	public AemterEbeneDialog AemterEbeneDialog;
	public Hinterzimmer Hinterzimmer;
	public BeziehungenPflegenDialog BeziehungenPflegenDialog;
	public SoeldnerRaeuberKarte SoeldnerRaeuberKarte;
	public StuetzpunktKaufenDialog StuetzpunktKaufenDialog;
	public StuetzpunktVerwalten StuetzpunktVerwalten;
	public KartenspielDialog KartenspielDialog;
	public GerichtDialog GerichtDialog;
	public TitelVerleihDialog TitelVerleihDialog;
	public OptionenDialog OptionenDialog;
	public CreditsDialog CreditsDialog;
	public HandelszertifikatDialog HandelszertifikatDialog;
	public LagerraumKaufenDialog LagerraumKaufenDialog;
	public StatistikDialog StatistikDialog;
	public TippsDialog TippsDialog;
	public StadtInformationenDialog StadtInformationenDialog;
	public NaechsterSpielerDialog NaechsterSpielerDialog;
	public SchuldturmDialog SchuldturmDialog;
	public SchuldenProzessDialog SchuldenProzessDialog;
	public AmtsenthebungDialog AmtsenthebungDialog;
	public HochzeitDialog HochzeitDialog;
	public RundenNachrichtenDialog RundenNachrichtenDialog;
	public JahresbuchDialog JahresbuchDialog;
	public IngameMenuDialog IngameMenuDialog;
	public CheatDialog CheatDialog;
	public ProfilDialog ProfilDialog;
	public KontrahentDetailsDialog KontrahentDetailsDialog;
	public AhnentafelDialog AhnentafelDialog;
	public SpielerTodDialog SpielerTodDialog;
	public KindestodDialog KindestodDialog;
	public DiagnoseDialog DiagnoseDialog;
	public AuftragSiegDialog AuftragSiegDialog;
	public BestenlisteDialog BestenlisteDialog;
	public DuellDialog DuellDialog;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		VerdrahteKnoten();

		// YesNoQuestion und ShowText werden nur an die Lib übergeben und brauchen kein eigenes Feld.
		var yesNoDialog = GetNode<YesNoDialog>(nameof(YesNoDialog));
		var textDialog = GetNode<TextDialog>(nameof(TextDialog));

		// Die Erpressungs-Entscheidung (Issue #13) läuft über den Ja/Nein-Dialog und braucht kein Feld.
		var erpressungDialog = GetNode<ErpressungDialog>(nameof(ErpressungDialog));

		// Alle aktionierbaren Privilegien-Dialoge sind jetzt echte Dialoge (kein Platzhalter mehr).
		SW.UI.Initialisieren(yesNoDialog, textDialog, BeziehungenPflegenDialog, BauwerkStiftenDialog, FestGebenDialog,
			Weltkarte, TestamentDialog, ProzentwertFestlegenDialog, UntergebeneDialog, DuellDialog, erpressungDialog);
	}

	/// <summary>
	/// Verdrahtet alle öffentlichen Node-Felder automatisch mit dem gleichnamigen Kindknoten
	/// (Konvention: Feld-/Typname == Node-Name). Ersetzt die früher pro Dialog nötigen
	/// [Export]-NodePath-Eigenschaften samt GetNode-Aufrufen – ein neuer Dialog braucht nur noch ein
	/// öffentliches Feld und den gleichnamigen Knoten in Main.tscn.
	/// </summary>
	private void VerdrahteKnoten()
	{
		foreach (var feld in typeof(Main).GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
		{
			if (!typeof(Node).IsAssignableFrom(feld.FieldType))
				continue;

			var knoten = GetNodeOrNull(feld.FieldType.Name);
			if (knoten != null)
				feld.SetValue(this, knoten);
			else
				GD.PushError($"Main: Kein Kindknoten \"{feld.FieldType.Name}\" gefunden – Feld bleibt null.");
		}
	}
}
