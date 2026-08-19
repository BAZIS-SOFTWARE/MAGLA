using MaterialDB.MaterialData.MetallurgicalData;
using PropertiesCalculator.PropertiesCalculator.MetallurgicalModels;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace PropertiesCalculator.PropertiesController.MetallurgicalModels
{
    /// <summary>
    /// ReactionType
    /// </summary>
    public enum ReactionType
    {
        /// <summary>
        /// speedAndTimeDelay
        /// </summary>
        speedAndTimeDelay,
        /// <summary>
        /// timeDelay
        /// </summary>
        timeDelay
    }
    /// <summary>
    /// MetallurgicalData
    /// </summary>
    public class MetallurgicalModel
    {
        /// <summary>
        /// MaterialName
        /// </summary>
        public string MaterialName { get; }
        /// <summary>
        /// PhaseData
        /// </summary>
        //public PhaseData PhaseData { get; } = new PhaseData();
        /// <summary>
        /// AvramiModel
        /// </summary>
        public Avrami AvramiModel { get; set; } = new Avrami();
        /// <summary>
        /// KostinenModel
        /// </summary>
        public Kostinen KostinenModel { get; set; } = new Kostinen();
        /// <summary>
        /// ProcessData
        /// </summary>
        //public ProcessData<float> ProcessData { get; } = new ProcessData<float>();

        /// <summary>
        /// Calc phase portion when heating, cooling, constant process
        /// </summary>
        /// <param name="temp"></param>
        /// <param name="timeStep"></param>
        /// <returns></returns>
        public void Calc(float temp, float timeStep, PhaseData phaseData, ProcessData processData)
        {
            foreach (var process in processData)
            {
                if (
                    temp <= process.TempMax &
                    temp >= process.TempMin
                    )

                {
                    //process.Time += TimeComputation(temp, tempVel, timeStep, process.TempMax, process.TempMin);
                    //------ Сделать проверку когда создаются процессы!

                    var phaseNames = process.Name.Split(' ')[1].Split('-');

                    var phExist = phaseData.Find(phaseNames[0]);
                    //if (phaseExist == null)
                    //    throw new Exception($"Не найдена фаза {phaseNames[0]}");
                    var phCreate = phaseData.Find(phaseNames[1]);
                    //    throw new Exception($"Не найдена фаза {phaseNames[1]}");

                    if (phExist.Value > process.PhaseMin & phCreate.Value < process.PhaseMax)
                    {
                        var phDelta = 0.0f;
                        if (process.DataTable.Columns.Count == 2)
                        {
                            var phase = KostinenModel.Calc(process.DataTable, temp);

                            phDelta = phase - phCreate.Value;


                        }
                        else
                        {
                            // это условие нужно. Это начальное условие процесса
                            if (phCreate.Value == 0)
                                phCreate.Value = process.PhaseMin;

                            var phVel = AvramiModel.Calc(process.DataTable, temp, phExist.Value, phCreate.Value);

                            // На данный момент это костыль до выяснения причин появления Nan
                            // Вероятные причины - слишком большие скорости нагрева или охлаждения для реакций
                            // Слишком маленькие значения мин фазы и коэффициенты близкие к 1
                            // Вероятно проблемы при усечении значений при счете. Лучше перейти на double
                            // Добавить различные тесты на критические значения начальных и конечных фаз
                            if(float.IsNaN(phVel))
                                phVel = 0.0f;

                            //var der_phase = MetallurgicalModel.Calc(reaction.DataTable, temp, process.Time);
                            phDelta = phVel * timeStep;
                        }
                        // отрицательный потенциал, реакция пошла в обратную сторону
                        if (phDelta < 0)
                            continue;

                        if (phDelta > phExist.Value)
                            phDelta = phExist.Value;

                        phCreate.Value += phDelta;
                        phExist.Value -= phDelta;

                        if (phExist.Value < process.PhaseMin)
                            phExist.Value = process.PhaseMin;
                        if (phCreate.Value > process.PhaseMax)
                            phCreate.Value = process.PhaseMax;
                    }

                }
            }
        }
        /// <summary>
        /// TimeComputation. Вычисление времени реакции с учетом входа снизу или сверху в интервалы реакции
        /// для полностью неявного расчета
        /// </summary>
        /// <param name="temp"></param>
        /// <param name="tempVel"></param>
        /// <param name="timeStep"></param>
        /// <param name="TempMax"></param>
        /// <param name="TempMin"></param>
        /// <returns></returns>
        private float TimeComputation(float temp, float tempVel, float timeStep, float TempMax, float TempMin)
        {
            var dTemp = tempVel * timeStep;
            //вычисление предыдущей температуры, так как расчет полностью неявный
            var preTemp = temp - dTemp;

            if (preTemp > TempMax)
                return (temp - TempMax) / tempVel;
            else if (preTemp < TempMin)
                return (temp - TempMin) / tempVel;
            else
                return timeStep;
        }
    }
}
