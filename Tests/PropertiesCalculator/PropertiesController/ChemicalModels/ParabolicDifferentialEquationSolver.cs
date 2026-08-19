using System;
using System.Collections.Generic;
using System.Linq;

namespace PropertiesCalculator.PropertiesController.ChemicalModels
{
    public class ParabolicDifferentialEquationSolver
    {
        private readonly Func<double, double> coefficient;
        private readonly Func<double, double> initialCondition;
        private readonly Func<double, double> leftBoundFunction;
        private readonly Func<double, double> rightBoundFunction;
        private const int pointsX = 20;

        public double StepX { get; }

        public ParabolicDifferentialEquationSolver(double rodLength,
            Func<double, double> leftBoundFunction,
            Func<double, double> rightBoundFunction,
            Func<double, double> coefficient,
            Func<double, double> initialCondition)
        {
            this.initialCondition = initialCondition;
            this.leftBoundFunction = leftBoundFunction;
            this.rightBoundFunction = rightBoundFunction;
            this.coefficient = coefficient;
            StepX = rodLength / pointsX;
        }

        public Dictionary<double, double> GetSolutionOnTime(double time)
        {
            var timePoints = GetTimePointsCount(time);
            var timeStep = time / timePoints;
            var previous = PreprocessInitialCondition().ToList();
            previous[0] = leftBoundFunction(0);
            previous[pointsX] = rightBoundFunction(0);
            var currentTime = timeStep;
            for (var i = 1; i <= timePoints; i++)
            {
                var r = (timeStep * coefficient(currentTime)) / (StepX * StepX);
                var next = Enumerable.Repeat(0d, pointsX + 1).ToList();
                next[0] = leftBoundFunction(currentTime);
                next[pointsX] = rightBoundFunction(currentTime);
                for (var j = 1; j < pointsX; j++)
                {
                    next[j] = r * previous[j - 1] + (1 - 2 * r) * previous[j] + r * previous[j + 1];
                }
                previous = next;
                currentTime += timeStep;
            }
            return GetXPoints()
                .Zip(previous, Tuple.Create)
                .ToDictionary(t => t.Item1, t => t.Item2);
        }

        private int GetTimePointsCount(double time)
        {
            return (int)Math.Ceiling(6 * time * coefficient(time) / (StepX * StepX)) + 1;
        }

        private IEnumerable<double> PreprocessInitialCondition()
        {
            return GetXPoints()
                .Select(x => initialCondition(x));
        }

        private IEnumerable<double> GetXPoints()
        {
            var currentX = 0d;
            for (var i = 0; i < pointsX + 1; i++)
            {
                yield return currentX;
                currentX += StepX;
            }
        }
    }
}