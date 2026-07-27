using Conspiratio.Lib.Gameplay.Spielwelt;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

public partial class Main : Control
{
	[Export]
	public NodePath YesNoDialogPath { get; set; }

	[Export]
	public NodePath TextDialogPath { get; set; }

	[Export]
	public NodePath LocalGameDialogPath { get; set; }
	
	[Export]
	public NodePath NewLocalGameMenuPath { get; set; }

	[Export]
	public NodePath NewPlayerMenuPath { get; set; }

	[Export]
	public NodePath KontorPath { get; set; }

	[Export]
	public NodePath AbrechnungDialogPath { get; set; }

	[Export]
	public NodePath StadtPath { get; set; }

	[Export]
	public NodePath LoadGameDialogPath { get; set; }

	[Export]
	public NodePath SchreibstubePath { get; set; }

	[Export]
	public NodePath KreditbuchDialogPath { get; set; }

	[Export]
	public NodePath GesetzeDialogPath { get; set; }

	[Export]
	public NodePath WeltkartePath { get; set; }

	[Export]
	public NodePath HausVerwaltungDialogPath { get; set; }

	[Export]
	public NodePath HausWaehlenDialogPath { get; set; }

	[Export]
	public NodePath HausErweiterungenDialogPath { get; set; }

	[Export]
	public NodePath BewerbDialogPath { get; set; }

	[Export]
	public NodePath BewerbInfosDialogPath { get; set; }

	[Export]
	public NodePath WahlDialogPath { get; set; }

	[Export]
	public NodePath KirchePath { get; set; }

	[Export]
	public NodePath TestamentDialogPath { get; set; }

	[Export]
	public NodePath BrautwerbungDialogPath { get; set; }

	[Export]
	public NodePath GeburtDialogPath { get; set; }

	[Export]
	public NodePath KirchgangPath { get; set; }

	[Export]
	public NodePath KonfessionslosDialogPath { get; set; }

	[Export]
	public NodePath PrivilegienDialogPath { get; set; }

	[Export]
	public NodePath FestGebenDialogPath { get; set; }

	[Export]
	public NodePath BauwerkStiftenDialogPath { get; set; }

	[Export]
	public NodePath ProzentwertFestlegenDialogPath { get; set; }

	[Export]
	public NodePath UntergebeneDialogPath { get; set; }

	[Export]
	public NodePath RohstoffpreiseDialogPath { get; set; }

	[Export]
	public NodePath KontrahentenDialogPath { get; set; }

	[Export]
	public NodePath AemterEbeneDialogPath { get; set; }

	[Export]
	public NodePath HinterzimmerPath { get; set; }

	[Export]
	public NodePath BeziehungenPflegenDialogPath { get; set; }

	[Export]
	public NodePath SoeldnerRaeuberKartePath { get; set; }

	[Export]
	public NodePath StuetzpunktKaufenDialogPath { get; set; }

	[Export]
	public NodePath StuetzpunktVerwaltenPath { get; set; }

	[Export]
	public NodePath KartenspielDialogPath { get; set; }

	[Export]
	public NodePath GerichtDialogPath { get; set; }

	[Export]
	public NodePath TitelVerleihDialogPath { get; set; }

	[Export]
	public NodePath OptionenDialogPath { get; set; }

	[Export]
	public NodePath CreditsDialogPath { get; set; }

	[Export]
	public NodePath HandelszertifikatDialogPath { get; set; }

	[Export]
	public NodePath LagerraumKaufenDialogPath { get; set; }

	[Export]
	public NodePath StatistikDialogPath { get; set; }

	[Export]
	public NodePath TippsDialogPath { get; set; }

	[Export]
	public NodePath StadtInformationenDialogPath { get; set; }

	[Export]
	public NodePath NaechsterSpielerDialogPath { get; set; }

	[Export]
	public NodePath IngameMenuDialogPath { get; set; }

	public LocalGameDialog LocalGameDialog;
	public NewLocalGameMenu NewLocalGameMenu;
	public NewPlayerMenu NewPlayerMenu;
	public Kontor Kontor;
	public AbrechnungDialog AbrechnungDialog;
	public Stadt Stadt;
	public LoadGameDialog LoadGameDialog;
	public Schreibstube Schreibstube;
	public KreditbuchDialog KreditbuchDialog;
	public GesetzeDialog GesetzeDialog;
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
	public IngameMenuDialog IngameMenuDialog;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		var yesNoDialog = GetNode<YesNoDialog>(YesNoDialogPath);
		var textDialog = GetNode<TextDialog>(TextDialogPath);
		LocalGameDialog = GetNode<LocalGameDialog>(LocalGameDialogPath);
		NewLocalGameMenu = GetNode<NewLocalGameMenu>(NewLocalGameMenuPath);
		NewPlayerMenu = GetNode<NewPlayerMenu>(NewPlayerMenuPath);
		Kontor = GetNode<Kontor>(KontorPath);
		AbrechnungDialog = GetNode<AbrechnungDialog>(AbrechnungDialogPath);
		Stadt = GetNode<Stadt>(StadtPath);
		LoadGameDialog = GetNode<LoadGameDialog>(LoadGameDialogPath);
		Schreibstube = GetNode<Schreibstube>(SchreibstubePath);
		KreditbuchDialog = GetNode<KreditbuchDialog>(KreditbuchDialogPath);
		GesetzeDialog = GetNode<GesetzeDialog>(GesetzeDialogPath);
		Weltkarte = GetNode<Weltkarte>(WeltkartePath);
		HausVerwaltungDialog = GetNode<HausVerwaltungDialog>(HausVerwaltungDialogPath);
		HausWaehlenDialog = GetNode<HausWaehlenDialog>(HausWaehlenDialogPath);
		HausErweiterungenDialog = GetNode<HausErweiterungenDialog>(HausErweiterungenDialogPath);
		BewerbDialog = GetNode<BewerbDialog>(BewerbDialogPath);
		BewerbInfosDialog = GetNode<BewerbInfosDialog>(BewerbInfosDialogPath);
		WahlDialog = GetNode<WahlDialog>(WahlDialogPath);
		Kirche = GetNode<Kirche>(KirchePath);
		TestamentDialog = GetNode<TestamentDialog>(TestamentDialogPath);
		BrautwerbungDialog = GetNode<BrautwerbungDialog>(BrautwerbungDialogPath);
		GeburtDialog = GetNode<GeburtDialog>(GeburtDialogPath);
		Kirchgang = GetNode<Kirchgang>(KirchgangPath);
		KonfessionslosDialog = GetNode<KonfessionslosDialog>(KonfessionslosDialogPath);
		PrivilegienDialog = GetNode<PrivilegienDialog>(PrivilegienDialogPath);
		FestGebenDialog = GetNode<FestGebenDialog>(FestGebenDialogPath);
		BauwerkStiftenDialog = GetNode<BauwerkStiftenDialog>(BauwerkStiftenDialogPath);
		ProzentwertFestlegenDialog = GetNode<ProzentwertFestlegenDialog>(ProzentwertFestlegenDialogPath);
		UntergebeneDialog = GetNode<UntergebeneDialog>(UntergebeneDialogPath);
		RohstoffpreiseDialog = GetNode<RohstoffpreiseDialog>(RohstoffpreiseDialogPath);
		KontrahentenDialog = GetNode<KontrahentenDialog>(KontrahentenDialogPath);
		AemterEbeneDialog = GetNode<AemterEbeneDialog>(AemterEbeneDialogPath);
		Hinterzimmer = GetNode<Hinterzimmer>(HinterzimmerPath);
		BeziehungenPflegenDialog = GetNode<BeziehungenPflegenDialog>(BeziehungenPflegenDialogPath);
		SoeldnerRaeuberKarte = GetNode<SoeldnerRaeuberKarte>(SoeldnerRaeuberKartePath);
		StuetzpunktKaufenDialog = GetNode<StuetzpunktKaufenDialog>(StuetzpunktKaufenDialogPath);
		StuetzpunktVerwalten = GetNode<StuetzpunktVerwalten>(StuetzpunktVerwaltenPath);
		KartenspielDialog = GetNode<KartenspielDialog>(KartenspielDialogPath);
		GerichtDialog = GetNode<GerichtDialog>(GerichtDialogPath);
		TitelVerleihDialog = GetNode<TitelVerleihDialog>(TitelVerleihDialogPath);
		OptionenDialog = GetNode<OptionenDialog>(OptionenDialogPath);
		CreditsDialog = GetNode<CreditsDialog>(CreditsDialogPath);
		HandelszertifikatDialog = GetNode<HandelszertifikatDialog>(HandelszertifikatDialogPath);
		LagerraumKaufenDialog = GetNode<LagerraumKaufenDialog>(LagerraumKaufenDialogPath);
		StatistikDialog = GetNode<StatistikDialog>(StatistikDialogPath);
		TippsDialog = GetNode<TippsDialog>(TippsDialogPath);
		StadtInformationenDialog = GetNode<StadtInformationenDialog>(StadtInformationenDialogPath);
		NaechsterSpielerDialog = GetNode<NaechsterSpielerDialog>(NaechsterSpielerDialogPath);
		IngameMenuDialog = GetNode<IngameMenuDialog>(IngameMenuDialogPath);

		// Alle aktionierbaren Privilegien-Dialoge sind jetzt echte Dialoge (kein Platzhalter mehr).
		SW.UI.Initialisieren(yesNoDialog, textDialog, BeziehungenPflegenDialog, BauwerkStiftenDialog, FestGebenDialog,
			Weltkarte, TestamentDialog, ProzentwertFestlegenDialog, UntergebeneDialog);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}