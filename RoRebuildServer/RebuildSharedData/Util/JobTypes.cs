using System;
using System.Collections.Generic;
using System.Text;
using RebuildSharedData.Enum;

namespace RebuildSharedData.Util;

public static class JobTypes
{
    public static bool IsBaseJob(int id, JobType type) => IsBaseJob((JobType)id, type);
    public static bool IsBaseJob(JobType t, JobType type)
    {
        switch (type)
        {
            case JobType.JobNovice: return true;
            case JobType.JobSwordsman:
                return t is JobType.JobSwordsman or JobType.JobKnight or JobType.JobPecoKnight or JobType.JobCrusader
                    or JobType.JobPecoCrusader;
            case JobType.JobAcolyte: 
                return t is JobType.JobAcolyte or JobType.JobPriest or JobType.JobMonk;
            case JobType.JobArcher:
                return t is JobType.JobArcher or JobType.JobHunter or JobType.JobDancer or JobType.JobBard;
            case JobType.JobMage:
                return t is JobType.JobMage or JobType.JobWizard or JobType.JobSage;
            case JobType.JobThief:
                return t is JobType.JobThief or JobType.JobAssassin or JobType.JobRogue;
            case JobType.JobMerchant:
                return t is JobType.JobMerchant or JobType.JobBlacksmith or JobType.JobAlchemist;
            case JobType.JobKnight:
                return t is JobType.JobKnight or JobType.JobPecoKnight;
            case JobType.JobCrusader:
                return t is JobType.JobCrusader or JobType.JobPecoCrusader;
            default:
                return t == type;
        }
    }
}