import { defaultPreparationInput, emptyPreparationProgress, PreparationPack } from '../models/interview-prep.model';

export function preparationPack(): PreparationPack {
  return {
    id: 'pack-1', scrapeResultId: null, jobTitle: null, companyName: null,
    input: { ...defaultPreparationInput(), targetRole: 'Product designer', targetLevel: 'Mid-level' },
    content: {
      title: 'Design preparation', overviewMarkdown: 'Start with [the foundations](foundations.md).', unknowns: ['Interview format is unknown.'],
      topics: [{ name: 'Design reasoning', relevance: 'The role requires tradeoffs.', evidence: 'Exposure reported in the CV.', documentId: 'foundations' }],
      documents: [{ id: 'foundations', title: 'Foundations', purpose: 'Understand the mechanism.', markdown: '# Follow the constraint\n\nA complete explanation of the design decision.', prerequisiteIds: [] }],
      studyPlan: [{ day: 1, minutes: 30, activity: 'Study, attempt, verify, and explain a decision.', documentIds: ['foundations'] }]
    },
    progress: emptyPreparationProgress(), needsRegeneration: false, outdatedReasons: [], isDemo: false,
    generatedAt: '2026-11-26T10:00:00Z', updatedAt: '2026-11-26T10:00:00Z'
  };
}

export function discoveryPack(): PreparationPack {
  const pack = preparationPack();
  const content = pack.content!;
  return {
    ...pack,
    content: { ...content, documents: content.documents.map(d => ({ ...d, markdown: '', isGenerated: false })) },
    discovery: {
      documents: content.documents.map(d => ({ ...d, questions: [
        { id: 'foundations-needs', kind: 'knowledge', prompt: 'Where do you need a clearer explanation?',
          whyItMatters: 'This determines the depth and examples.', guidanceMarkdown: 'Start with one task you have tried.',
          answerStarter: 'I can ___. I get stuck when ___.', hypotheticalExample: 'A fictional learner understands a definition but needs a worked example.' },
        { id: 'foundations-contribution', kind: 'experience', prompt: 'Which part did you personally handle?',
          whyItMatters: 'This keeps project claims grounded.', guidanceMarkdown: 'Look at a ticket or your notes. Separate your work from team work.',
          answerStarter: 'My part was ___. Someone else handled ___.', hypotheticalExample: 'A fictional contributor owned a form while another person handled deployment.' }
      ] })),
      answers: [], contextRevision: 0
    }
  };
}
