import { IsoDate } from '../../../core/types/iso-date';
import { ActiveSubscription } from './active-subscription';

export type MonthSubscription = ActiveSubscription & { dueDate: IsoDate };
