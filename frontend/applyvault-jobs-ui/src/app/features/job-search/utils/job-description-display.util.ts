import { ExternalJobDetail } from '../models/external-job.model';

export type JobDescriptionDisplayMode = 'full' | 'preview' | 'empty';

type JobDescriptionDisplayInput = Pick<
  ExternalJobDetail,
  'description' | 'descriptionQuality' | 'descriptionExcerpt'
>;

export function resolveJobDescriptionDisplayMode(
  job: JobDescriptionDisplayInput | null | undefined
): JobDescriptionDisplayMode {
  if (!job) {
    return 'empty';
  }

  if (job.descriptionQuality === 'previewOnly') {
    return 'preview';
  }

  if (job.description?.trim()) {
    return 'full';
  }

  return 'empty';
}
