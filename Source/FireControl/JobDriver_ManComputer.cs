using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace FireControl;

public class JobDriver_ManComputer : JobDriver
{
    private CompFireControl compFireControl;
    public CompFireControl FireControl => compFireControl ??= job.targetA.Thing.TryGetComp<CompFireControl>();
    public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed);

    public override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
        AddEndCondition(() => FireControl.Active ? JobCondition.Ongoing : JobCondition.Incompletable);
        yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);
        var toil = new Toil
        {
            tickAction = delegate
            {
                FireControl.Used(pawn);
                pawn.rotationTracker.FaceTarget(job.targetA);
            },
            handlingFacing = true,
            defaultCompleteMode = ToilCompleteMode.Never
        };
        toil.FailOnCannotTouch(TargetIndex.A, PathEndMode.InteractionCell);
        yield return toil;
    }
}
