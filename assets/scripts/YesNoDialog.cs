using System.Threading.Tasks;
using Conspiratio.Lib.Allgemein;
using Godot;

namespace Conspiratio.Godot.assets.scripts;

public partial class YesNoDialog : DialogBase, IYesNoQuestion
{
	[Export]
	public NodePath LabelQuestionPath { get; set; }

	[Export]
	public NodePath LinkButtonYesPath { get; set; }

	[Export]
	public NodePath LinkButtonNoPath { get; set; }

	private Label _labelQuestion;
	private controls.LinkButtonWithSounds _linkButtonYes;
	private controls.LinkButtonWithSounds _linkButtonNo;

	protected override void OnReady()
	{
		_labelQuestion = GetNode<Label>(LabelQuestionPath);
		_linkButtonYes = GetNode<controls.LinkButtonWithSounds>(LinkButtonYesPath);
		_linkButtonNo = GetNode<controls.LinkButtonWithSounds>(LinkButtonNoPath);
	}

	public Task<DialogResultGame> ShowDialogText(string textQuestion, string textYes = "Ja", string textNo = "Nein")
	{
		_labelQuestion.Text = textQuestion;
		_linkButtonYes.Text = textYes;
		_linkButtonNo.Text = textNo;

		return ShowAndAwait();
	}

	private void _on_link_button_yes_pressed()
	{
		Close(DialogResultGame.Yes);
	}

	private void _on_link_button_no_pressed()
	{
		Close(DialogResultGame.No);
	}
}
