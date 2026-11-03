using System;
using System.Collections.Generic;
using System.Linq;
using InvoiceGenerator.Models;

namespace InvoiceGenerator.Discrepancies
{
    /// <summary>
    /// Chooses the discrepancy for a run and applies it: a pinned id always, otherwise one from the exercise
    /// set with the given probability, otherwise nothing. At most one per invoice — a reviewer learns nothing
    /// from three problems that one does not teach.
    /// </summary>
    public sealed class Engine
    {
        private readonly List<IInjector> _all;

        public Engine(IEnumerable<IInjector> catalogue) { _all = catalogue.ToList(); }

        public IReadOnlyList<IInjector> Catalogue => _all;
        public IEnumerable<IInjector> ExerciseSet => _all.Where(i => i.InExerciseSet);

        /// <summary>Resolves the request. An unknown pin throws rather than falling back to random, so a typo cannot quietly invalidate a test.</summary>
        public IInjector Choose(string pinnedId, int probability, Random rng)
        {
            if (!string.IsNullOrWhiteSpace(pinnedId))
            {
                var wanted = pinnedId.Trim();
                if (wanted.Equals("none", StringComparison.OrdinalIgnoreCase) || wanted.Equals("clean", StringComparison.OrdinalIgnoreCase)) return null;
                var hit = _all.FirstOrDefault(i => i.Id.Equals(wanted, StringComparison.OrdinalIgnoreCase));
                if (hit != null) return hit;
                throw new ArgumentException($"Unknown discrepancy '{pinnedId}'. Known ids: {string.Join(", ", _all.Select(i => i.Id))}; 'none' forces a clean pair.");
            }
            var p = Math.Clamp(probability, 0, 100);
            if (p == 0 || rng.Next(100) >= p) return null;
            var set = ExerciseSet.ToList();
            return set[rng.Next(set.Count)];
        }

        /// <summary>Applies the injector (if any), recalculates the invoice and returns the expected outcome.</summary>
        public ExpectedOutcome Apply(IInjector injector, InjectionContext ctx)
        {
            if (injector == null)
                return new ExpectedOutcome { Status = Status.Green, DeterministicMatch = true };

            injector.Inject(ctx);
            ctx.Invoice.Recalculate();
            foreach (var f in ctx.Findings) if (string.IsNullOrEmpty(f.Severity)) f.Severity = injector.Expected;
            return new ExpectedOutcome { Status = injector.Expected, DeterministicMatch = false, Findings = ctx.Findings.ToList() };
        }
    }
}
