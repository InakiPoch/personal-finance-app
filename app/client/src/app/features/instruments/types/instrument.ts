import { IsoDate } from '../../../core/types/iso-date';
import { InstrumentType } from '../../../core/types/instrument-type';

export type Instrument = {
  id: string;
  type: InstrumentType;
  name: string;
  cutoffDate: number | null;
  /** Next closing date; only set for credit cards. */
  nextClosingDate: IsoDate | null;
};
