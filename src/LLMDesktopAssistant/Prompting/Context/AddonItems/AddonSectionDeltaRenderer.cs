using LLMDesktopAssistant.LLM.Services;

namespace LLMDesktopAssistant.Prompting.Context.AddonItems
{
	/// <summary>
	/// The base delta renderer of an addon section: builds the standard template context
	/// (added / removed / became_visible / updated lists) and renders the section template.
	/// The section supplies the template id and the per-item projections.
	/// </summary>
	public abstract class AddonSectionDeltaRenderer<TItem, TChange, TDelta>(
		ITemplateLibraryAccessor templates
	) : IPromptSectionDeltaRenderer<TDelta>
		where TItem : AddonSectionItem
		where TChange : AddonItemChange<TItem>
		where TDelta : AddonSectionDelta<TItem, TChange>
	{
		/// <inheritdoc/>
		public string Render(TDelta delta)
		{
			return templates.GetTextTemplate(TemplateId).Render(new
			{
				added = delta.AddedItems.Count > 0
					? delta.AddedItems.Select(ProjectAddition).ToArray()
					: null,
				removed = delta.RemovedItems.Count > 0
					? delta.RemovedItems.Select(ProjectRemoval).ToArray()
					: null,
				became_visible = delta.BecameVisibleItems.Count > 0
					? delta.BecameVisibleItems.Select(ProjectBecameVisible).ToArray()
					: null,
				updated = delta.UpdatedItems.Count > 0
					? delta.UpdatedItems.Select(ProjectUpdate).ToArray()
					: null
			});
		}

		/// <summary>
		/// The id of the section delta template (for example 'tools_system_section_delta').
		/// </summary>
		protected abstract string TemplateId { get; }

		/// <summary>
		/// Projects an addition into the template context object.
		/// </summary>
		protected abstract object ProjectAddition(AddonItemAddition<TItem, TChange> addition);

		/// <summary>
		/// Projects a removal into the template context object.
		/// </summary>
		protected abstract object ProjectRemoval(AddonItemRemoval removal);

		/// <summary>
		/// Projects a visibility gain into the template context object.
		/// </summary>
		protected abstract object ProjectBecameVisible(AddonItemBecameVisible<TItem, TChange> becameVisible);

		/// <summary>
		/// Projects a definition update into the template context object.
		/// </summary>
		protected abstract object ProjectUpdate(AddonItemUpdate<TChange> update);
	}
}
