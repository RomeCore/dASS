using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.Addons.MVVM.Elements;
using LLMDesktopAssistant.Controls.Dialogs;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Services.Instances;
using Material.Icons;

namespace LLMDesktopAssistant.Addons.MVVM
{
	/// <summary>
	/// The base of every addon card factory. Builds all elements that do not depend on the concrete addon
	/// type: the enable/hide override editors, the source, category and diagnostic chips, the tag chips,
	/// the path label and the file actions. The type-specific elements are added by the derived factory
	/// through the <c>Add*</c> hooks, which are called in a fixed order.
	/// </summary>
	/// <typeparam name="TAddon">The type of the addon the factory builds cards for.</typeparam>
	/// <typeparam name="TChange">The type of the change (override) object of that addon.</typeparam>
	public abstract class AddonCardFactoryBase<TAddon, TChange> : IAddonCardFactory<TAddon, TChange>
		where TAddon : AddonChangedBase<TAddon, TChange>
		where TChange : AddonChangeBase, new()
	{
		/// <summary>The order of the override editors shown in the card header.</summary>
		protected const int HeaderOrder = 0;

		/// <summary>The order of the chips.</summary>
		protected const int ChipOrder = 100;

		/// <summary>The order of the tag chips.</summary>
		protected const int TagOrder = 200;

		/// <summary>The order of the blocks.</summary>
		protected const int BlockOrder = 300;

		/// <summary>The order of the elements shown at the left side of the action row.</summary>
		protected const int ActionRowOrder = 400;

		/// <summary>The order of the action buttons.</summary>
		protected const int ActionOrder = 500;

		/// <summary>
		/// Gets the icon shown in the header of the cards of the addon type.
		/// </summary>
		protected abstract MaterialIconKind TypeIcon { get; }

		private readonly IExplorerOpener? _explorerOpener;
		private readonly IToastService? _toastService;

		/// <summary>
		/// Initializes a new instance of the <see cref="AddonCardFactoryBase{TAddon, TChange}"/> class.
		/// </summary>
		/// <param name="explorerOpener">The service used by the "show in explorer" action.</param>
		/// <param name="toastService">The service used to report failures of the file actions.</param>
		protected AddonCardFactoryBase(IExplorerOpener? explorerOpener = null, IToastService? toastService = null)
		{
			_explorerOpener = explorerOpener;
			_toastService = toastService;
		}

		/// <inheritdoc/>
		public AddonCardViewModel Create(AddonCardContext<TAddon, TChange> context)
		{
			ArgumentNullException.ThrowIfNull(context);

			var elements = new List<IAddonCardElement>();

			AddHeaderChanges(context, elements);
			AddHeaderElements(context, elements);
			AddChips(context, elements);
			AddTags(context, elements);
			AddBlocks(context, elements);
			AddActionRowElements(context, elements);
			AddActions(context, elements);
			AddTypeActions(context, elements, ActionOrder + 100);

			var (namePrefix, namePrefixBrush) = GetNamePrefix(context);

			return new AddonCardViewModel(elements)
			{
				Icon = context.Addon.Icon ?? TypeIcon,
				Name = context.Addon.NameKey,
				NamePrefix = namePrefix,
				NamePrefixBrush = namePrefixBrush,
				Subtitle = GetSubtitle(context),
				Description = context.Addon.DescriptionKey,
				ResetCommand = new RelayCommand(context.Reset)
			};
		}

		/// <inheritdoc/>
		public AddonCardViewModel CreateGroup(AddonGroupCardContext<TAddon, TChange> context)
		{
			ArgumentNullException.ThrowIfNull(context);

			var elements = new List<IAddonCardElement>();

			AddGroupHeaderChanges(context, elements);
			AddGroupChips(context, elements);

			return new AddonCardViewModel(elements)
			{
				Icon = context.Key.Icon ?? TypeIcon,
				Name = context.Key.Title,
				NamePrefixBrush = context.Key.Brush,
				Children = context.Children,
				ResetCommand = new RelayCommand(() => ResetVisibleChildren(context.Children))
			};
		}

		/// <summary>
		/// Adds the aggregate change elements of the group card. Only header change elements are
		/// aggregated: by default the group gets the aggregate enabled/hidden toggles, while the
		/// type-specific aggregates (an approval level selector of the tools, a model selector of the
		/// sub-agents) are added by the derived factories.
		/// </summary>
		protected virtual void AddGroupHeaderChanges(AddonGroupCardContext<TAddon, TChange> context, List<IAddonCardElement> elements)
		{
			elements.Add(new AddonCardGroupEnabledHiddenChange(context.Children) { Order = HeaderOrder });
		}

		/// <summary>
		/// Adds the chips of the group card: by default the live count of the children.
		/// </summary>
		protected virtual void AddGroupChips(AddonGroupCardContext<TAddon, TChange> context, List<IAddonCardElement> elements)
		{
			elements.Add(new AddonCardGroupCountChip(context.Children) { Order = ChipOrder });
		}

		/// <summary>
		/// Resets all overrides of the visible children of the group.
		/// </summary>
		private static void ResetVisibleChildren(ImmutableList<AddonCardViewModel> children)
		{
			foreach (var child in children)
			{
				if (child.IsVisible && child.HasChanges)
					child.ResetCommand?.Execute(null);
			}
		}

		// =====================================================
		// === Header                                        ===
		// =====================================================

		/// <summary>
		/// Adds the "enabled" and "hidden" override editors of every addon.
		/// </summary>
		protected virtual void AddHeaderChanges(AddonCardContext<TAddon, TChange> context, List<IAddonCardElement> elements)
		{
			elements.Add(new AddonCardEnabledHiddenChange<TAddon, TChange>(context) { Order = HeaderOrder });
		}

		/// <summary>
		/// Hook: adds the type-specific override editors (selectors, model pickers, ...) to the card header.
		/// </summary>
		protected virtual void AddHeaderElements(AddonCardContext<TAddon, TChange> context, List<IAddonCardElement> elements)
		{
		}

		/// <summary>
		/// Gets the name prefix of the card. By default the card has none; the addon source is shown by
		/// the source chip instead.
		/// </summary>
		protected virtual (LocaleKeyBase? Prefix, IBrush? Brush) GetNamePrefix(AddonCardContext<TAddon, TChange> context)
		{
			return (null, null);
		}

		/// <summary>
		/// Gets the subtitle of the card: the addon identifier, but only when the display name of the addon
		/// differs from it. Returns <see langword="null"/> in every other case.
		/// </summary>
		protected virtual LocaleKeyBase? GetSubtitle(AddonCardContext<TAddon, TChange> context)
		{
			if (string.Equals(context.Addon.NameKey.Value, context.Addon.Name, StringComparison.Ordinal))
				return null;

			return Locale.GetConstKey(context.Addon.Name);
		}

		// =====================================================
		// === Chips                                         ===
		// =====================================================

		/// <summary>
		/// Adds the chips of the card: the source chip, the category chip, the diagnostic chips and the
		/// type-specific chips of the derived factory.
		/// </summary>
		protected virtual void AddChips(AddonCardContext<TAddon, TChange> context, List<IAddonCardElement> elements)
		{
			AddSourceChip(context, elements, ChipOrder);
			AddCategoryChip(context, elements, ChipOrder + 1);
			AddDiagnosticChips(context, elements, ChipOrder + 10);
			AddTypeChips(context, elements, ChipOrder + 100);
		}

		/// <summary>
		/// Adds the chip that shows where the addon comes from. It is drawn without a border and dimmed,
		/// so that it does not compete with the meaningful chips.
		/// </summary>
		protected virtual void AddSourceChip(AddonCardContext<TAddon, TChange> context, List<IAddonCardElement> elements, int order)
		{
			elements.Add(new AddonCardChip
			{
				Order = order,
				HasBorder = false,
				Opacity = 0.5,
				Icon = GetSourceIcon(context.Addon),
				Label = GetSourceLabel(context.Addon)
			});
		}

		/// <summary>
		/// Adds the chip that shows the category declared by the addon, if any.
		/// </summary>
		protected virtual void AddCategoryChip(AddonCardContext<TAddon, TChange> context, List<IAddonCardElement> elements, int order)
		{
			if (context.Addon.CategoryKey is not { } category)
				return;

			elements.Add(new AddonCardChip
			{
				Order = order,
				Icon = MaterialIconKind.FolderOutline,
				Label = category,
				ToolTip = Locale.GetKey("card.category")
			});
		}

		/// <summary>
		/// Adds one chip per diagnostic flag of the addon. The chip carries the severity color and the
		/// hint of the flag, so an addon with parsing problems is understandable right from the list.
		/// </summary>
		protected virtual void AddDiagnosticChips(AddonCardContext<TAddon, TChange> context, List<IAddonCardElement> elements, int order)
		{
			foreach (var flag in AddonDiagnosticFlagInfo.CreateFromDiagnostic(context.Addon.Diagnostic))
			{
				elements.Add(new AddonCardChip
				{
					Order = order++,
					Brush = flag.Color,
					Icon = flag.Icon,
					Label = Locale.GetConstKey(flag.DisplayName),
					ToolTip = string.IsNullOrWhiteSpace(flag.Description) ? null : Locale.GetConstKey(flag.Description)
				});
			}
		}

		/// <summary>
		/// Hook: adds the chips that describe the type-specific data of the addon (tool counts, linked
		/// skills, memory blocks, ...).
		/// </summary>
		protected virtual void AddTypeChips(AddonCardContext<TAddon, TChange> context, List<IAddonCardElement> elements, int order)
		{
		}

		// =====================================================
		// === Tags                                          ===
		// =====================================================

		/// <summary>
		/// Adds one chip per tag of the addon. When the host provides a tag command, the chips are
		/// clickable and let the user filter the list by the clicked tag.
		/// </summary>
		protected virtual void AddTags(AddonCardContext<TAddon, TChange> context, List<IAddonCardElement> elements)
		{
			var order = TagOrder;
			var command = context.TagClickCommand;

			foreach (var tag in context.Addon.Tags.Order(StringComparer.OrdinalIgnoreCase))
			{
				elements.Add(command is null
					? new AddonCardChip
					{
						Order = order++,
						Icon = MaterialIconKind.Tag,
						Label = Locale.GetConstKey(tag)
					}
					: new AddonCardTagChip
					{
						Order = order++,
						Icon = MaterialIconKind.Tag,
						Label = Locale.GetConstKey(tag),
						ToolTip = Locale.GetKey("card.tag.filter"),
						Command = command,
						CommandParameter = tag
					});
			}
		}

		// =====================================================
		// === Blocks                                        ===
		// =====================================================

		/// <summary>
		/// Hook: adds the blocks of the card using the <c>Add*Block</c> helpers of this class.
		/// </summary>
		protected virtual void AddBlocks(AddonCardContext<TAddon, TChange> context, List<IAddonCardElement> elements)
		{
		}

		/// <summary>
		/// Adds a collapsible block that edits the parameters declared by the parameter schema of the addon.
		/// Does nothing when the addon has no parameter schema.
		/// </summary>
		/// <param name="context">The card context.</param>
		/// <param name="elements">The element list to add the block to.</param>
		/// <param name="title">The title of the block, or <see langword="null"/> for the generic one.</param>
		/// <param name="order">The order of the block.</param>
		protected static void AddParametersBlock(AddonCardContext<TAddon, TChange> context, List<IAddonCardElement> elements,
			LocaleKeyBase? title = null, int order = BlockOrder)
		{
			if (context.Addon.ParameterSchema is null)
				return;

			elements.Add(new AddonCardParametersBlock<TAddon, TChange>(context)
			{
				Order = order,
				Title = title ?? Locale.GetKey("card.parameters")
			});
		}

		/// <summary>
		/// Adds a details block that shows a text, for example the body of the addon file.
		/// Does nothing when the text is empty.
		/// </summary>
		protected static void AddTextBlock(List<IAddonCardElement> elements, LocaleKeyBase title, string? text,
			int order = BlockOrder + 10)
		{
			if (string.IsNullOrWhiteSpace(text))
				return;

			elements.Add(new AddonCardBlock
			{
				Order = order,
				Title = title,
				Visibility = AddonCardBlockVisibility.Details,
				Content = new AddonCardBodyTextViewModel(text.Trim())
			});
		}

		/// <summary>
		/// Adds a details block with one chip per label. Does nothing when there are no labels.
		/// </summary>
		protected static void AddChipsBlock(List<IAddonCardElement> elements, LocaleKeyBase title, MaterialIconKind? icon,
			IEnumerable<string> labels, int order = BlockOrder + 20)
		{
			ImmutableList<IAddonCardChip>.Builder chips = ImmutableList.CreateBuilder<IAddonCardChip>();

			foreach (var label in labels)
				chips.Add(new AddonCardChip
				{
					Icon = icon,
					Label = Locale.GetConstKey(label)
				});

			if (chips.Count == 0)
				return;

			elements.Add(new AddonCardBlock
			{
				Order = order,
				Title = title,
				Visibility = AddonCardBlockVisibility.Details,
				Chips = chips.ToImmutable()
			});
		}

		/// <summary>
		/// Adds a details block with the metadata declared by the addon (both the known and the additional
		/// fields). Does nothing when the addon has no metadata.
		/// </summary>
		protected static void AddMetadataBlock(AddonCardContext<TAddon, TChange> context, List<IAddonCardElement> elements,
			LocaleKeyBase? title = null, int order = BlockOrder + 30)
		{
			ImmutableList<IAddonCardChip>.Builder chips = ImmutableList.CreateBuilder<IAddonCardChip>();

			foreach (var (type, value) in context.Addon.Metadata)
				chips.Add(new AddonCardChip
				{
					Icon = GetMetadataIcon(type),
					Label = Locale.GetConstKey($"{Locale.Get($"card.metadata.{type.ToString().ToLowerInvariant()}")}: {value}")
				});

			foreach (var (key, value) in context.Addon.AdditionalMetadata)
				chips.Add(new AddonCardChip
				{
					Icon = MaterialIconKind.CardText,
					Label = Locale.GetConstKey($"{key}: {value}")
				});

			if (chips.Count == 0)
				return;

			elements.Add(new AddonCardBlock
			{
				Order = order,
				Title = title ?? Locale.GetKey("card.metadata"),
				Visibility = AddonCardBlockVisibility.Details,
				Chips = chips.ToImmutable()
			});
		}

		// =====================================================
		// === Action row                                    ===
		// =====================================================

		/// <summary>
		/// Adds the path of the addon file to the left side of the action row, if the addon is file-based.
		/// </summary>
		protected virtual void AddActionRowElements(AddonCardContext<TAddon, TChange> context, List<IAddonCardElement> elements)
		{
			if (string.IsNullOrEmpty(context.Addon.Path))
				return;

			elements.Add(new AddonCardActionRowElement
			{
				Order = ActionRowOrder,
				Content = new AddonCardPathViewModel(context.Addon.Path)
			});
		}

		/// <summary>
		/// Adds the file actions of the addon: open the file, reveal it in the file explorer and delete it.
		/// Does nothing for addons that are not backed by an existing file.
		/// </summary>
		protected virtual void AddActions(AddonCardContext<TAddon, TChange> context, List<IAddonCardElement> elements)
		{
			var path = context.Addon.Path;

			if (string.IsNullOrEmpty(path) || !File.Exists(path))
				return;

			var order = ActionOrder;

			elements.Add(new AddonCardAction
			{
				Order = order++,
				Icon = MaterialIconKind.Pencil,
				ToolTip = OpenFileToolTip,
				Command = new RelayCommand(() => OpenFile(path))
			});

			elements.Add(new AddonCardAction
			{
				Order = order++,
				Icon = MaterialIconKind.FolderOpen,
				ToolTip = ShowInExplorerToolTip,
				Command = new RelayCommand(() => _explorerOpener?.ShowFileInExplorer(path))
			});

			elements.Add(new AddonCardAction
			{
				Order = order,
				Icon = MaterialIconKind.Delete,
				ToolTip = DeleteFileToolTip,
				Command = new AsyncRelayCommand(() => DeleteFileAsync(context))
			});
		}

		/// <summary>
		/// Hook: adds the type-specific action buttons after the file actions.
		/// </summary>
		protected virtual void AddTypeActions(AddonCardContext<TAddon, TChange> context, List<IAddonCardElement> elements, int order)
		{
		}

		// =====================================================
		// === File actions                                  ===
		// =====================================================

		/// <summary>Gets the tooltip of the "open file" action.</summary>
		protected virtual LocaleKeyBase OpenFileToolTip => Locale.GetKey("card.actions.open");

		/// <summary>Gets the tooltip of the "show in explorer" action.</summary>
		protected virtual LocaleKeyBase ShowInExplorerToolTip => Locale.GetKey("card.actions.explorer");

		/// <summary>Gets the tooltip of the "delete file" action.</summary>
		protected virtual LocaleKeyBase DeleteFileToolTip => Locale.GetKey("card.actions.delete");

		/// <summary>Gets the title of the delete confirmation dialog.</summary>
		protected virtual LocaleKeyBase DeleteDialogTitle => Locale.GetKey("card.delete.title");

		/// <summary>Gets the description of the delete confirmation dialog for the given addon.</summary>
		protected virtual LocaleKeyBase GetDeleteDialogDescription(TAddon addon) =>
			Locale.GetConstKey(Locale.Format("card.delete.confirm", addon.Path ?? addon.Name));

		private void OpenFile(string path)
		{
			try
			{
				Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
			}
			catch (Exception ex)
			{
				_toastService?.ShowError(Locale.Get("common.error"), ex.Message);
			}
		}

		private async Task DeleteFileAsync(AddonCardContext<TAddon, TChange> context)
		{
			var addon = context.Addon;
			var path = addon.Path;

			if (string.IsNullOrEmpty(path) || !File.Exists(path))
				return;

			var confirm = new ConfirmDialogViewModel
			{
				Title = DeleteDialogTitle.Value,
				Description = GetDeleteDialogDescription(addon).Value,
				ConfirmText = Locale.Get("common.delete"),
				CancelText = Locale.Get("common.cancel"),
				IsDanger = true
			};

			var result = await DialogManager.ShowDialogAsync(confirm);
			if (result is not bool confirmed || !confirmed)
				return;

			try
			{
				File.Delete(path);

				// A skill or a sub-agent lives in its own directory: remove it when the deleted file was its only content.
				if (addon.HomeDirectory is { Length: > 0 } home && Directory.Exists(home)
					&& !Directory.EnumerateFileSystemEntries(home).Any())
				{
					Directory.Delete(home);
				}

				context.OnDeleted?.Invoke();
			}
			catch (Exception ex)
			{
				_toastService?.ShowError(Locale.Get("common.error"), ex.Message);
			}
		}

		// =====================================================
		// === Helpers                                       ===
		// =====================================================

		/// <summary>
		/// Gets the icon that represents the source the addon was loaded from.
		/// </summary>
		protected static MaterialIconKind GetSourceIcon(TAddon addon) => addon.AddonSource switch
		{
			AddonSource.Pack => addon.SourcePack?.Source is AddonPackSource.AgentsHome or AddonPackSource.UserAgentsHome
				? MaterialIconKind.Folder
				: MaterialIconKind.PackageVariant,
			AddonSource.Template => MaterialIconKind.FileCode,
			_ => MaterialIconKind.HelpCircle
		};

		/// <summary>
		/// Gets the localized label of the source the addon was loaded from.
		/// </summary>
		protected static LocaleKeyBase GetSourceLabel(TAddon addon) =>
			Locale.GetKey($"card.source.{addon.AddonSource.ToString().ToLowerInvariant()}");

		private static MaterialIconKind GetMetadataIcon(AddonMetadataType type) => type switch
		{
			AddonMetadataType.Author => MaterialIconKind.Account,
			AddonMetadataType.License => MaterialIconKind.CardText,
			AddonMetadataType.Version => MaterialIconKind.Tag,
			_ => MaterialIconKind.Information
		};

	}
}
