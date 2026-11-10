import { normalizeToFixedSections } from '../models/cv-fixed-sections.model';
import { CvStructuredDocument, CvStructuredSection } from '../models/cv-structured.model';
import { cloneSectionForDraft, toSaveRequest } from './cv-structured-draft.util';

/**
 * Merge an Assist (AI update) proposal into the locally known structured CV.
 *
 * Decision: `ai-update-propose` returns proposed sections without persisting.
 * Approve merges by section id then saves via existing PUT:
 * - With focus ids: replace only those sections from the AI payload; keep all
 *   other local sections (order preserved). Ignore non-focused AI sections.
 * - Without focus: treat AI sections as authoritative by id; preserve any local
 *   sections the payload omitted; append AI-only new sections at the end.
 * Visibility is copied from the section being replaced before normalize, so an
 * Assist proposal cannot include an omitted Section by adding Entries.
 */
export function mergeAssistStructuredUpdate(
  previous: CvStructuredDocument | null,
  aiResult: CvStructuredDocument,
  focusSectionIds?: readonly string[]
): CvStructuredDocument {
  if (!previous || previous.sections.length === 0) {
    return {
      ...aiResult,
      sections: normalizeToFixedSections(aiResult.sections)
    };
  }

  const aiById = new Map(aiResult.sections.map((section) => [section.id, section]));
  const focusSet =
    focusSectionIds && focusSectionIds.length > 0 ? new Set(focusSectionIds) : null;

  let mergedSections: CvStructuredSection[];

  if (focusSet) {
    mergedSections = previous.sections.map((section) => {
      if (!focusSet.has(section.id)) {
        return cloneSectionForDraft(section);
      }

      const fromAi = aiById.get(section.id);
      return fromAi ? stampIncludedOnTemplate(fromAi, section) : cloneSectionForDraft(section);
    });
  } else {
    const previousIds = new Set(previous.sections.map((section) => section.id));
    mergedSections = previous.sections.map((section) => {
      const fromAi = aiById.get(section.id);
      return fromAi ? stampIncludedOnTemplate(fromAi, section) : cloneSectionForDraft(section);
    });

    for (const section of aiResult.sections) {
      if (!previousIds.has(section.id)) {
        mergedSections.push(cloneSectionForDraft(section));
      }
    }
  }

  return {
    documentId: aiResult.documentId || previous.documentId,
    structuredImportedAt: aiResult.structuredImportedAt ?? previous.structuredImportedAt,
    sections: normalizeToFixedSections(mergedSections)
  };
}

/** Visibility is seeker state. Copy it from the section being replaced. */
function stampIncludedOnTemplate(
  fromAi: CvStructuredSection,
  previous: CvStructuredSection
): CvStructuredSection {
  return {
    ...cloneSectionForDraft(fromAi),
    includedOnTemplate: previous.includedOnTemplate
  };
}

/**
 * True when the merged document differs from the raw AI/API payload enough that
 * the server (which already saved the AI body) needs a corrective PUT.
 */
export function assistMergeRequiresPersist(
  aiResult: CvStructuredDocument,
  merged: CvStructuredDocument
): boolean {
  return (
    JSON.stringify(toSaveRequest(aiResult.sections)) !==
    JSON.stringify(toSaveRequest(merged.sections))
  );
}
