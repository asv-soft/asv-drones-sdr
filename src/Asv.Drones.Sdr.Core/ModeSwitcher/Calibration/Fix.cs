using System.Collections;

namespace Asv.Drones.Sdr.Core;

public class PiecewiseLinearFunctionDelete:IEnumerable<KeyValuePair<double,double>>
    {
        private readonly double[,] _values;
        private readonly bool _isScaleForOnePoint;

        public PiecewiseLinearFunctionDelete(double[,] values, bool isScaleForOnePoint = false)
        {
            _values = values;
            _isScaleForOnePoint = isScaleForOnePoint;
        }

        public double this[double value]
        {
            get
            {
                // If values is empty, return F(x) = x
                if (_values.Length == 0) return value;

                if (double.IsNaN(value) || double.IsInfinity(value)) return value;

                // if contain 1 point, then 
                    if (_values.Length == 2)
                {
                    // if scale then return F(x) = k*x 
                    if (_isScaleForOnePoint)
                    {
                        return value * _values[0, 1] / _values[0, 0];
                    }
                    // if offset then return F(x) = b + x 
                    else
                    {
                        return _values[0, 1] - _values[0, 0] + value;
                    }
                }

                var first = true;
                double x2;
                double x3;
                double y2;
                double y3;
                double num;
                var prev = new KeyValuePair<double, double>();

                for (var i = 0; i < _values.Length/_values.Rank; i++)
                {
                    if (value < _values[i,0])
                    {
                        if (first)
                        {
                            x2 = _values[0,0];
                            x3 = _values[1,0];
                            y2 = _values[0,1];
                            y3 = _values[1,1];
                        }
                        else
                        {
                            x2 = prev.Key;
                            x3 = _values[i, 0];
                            y2 = prev.Value;
                            y3 = _values[i, 1];
                        }
                        num = (value - x2) / (x3 - x2);
                        return y2 + (y3 - y2) * num;
                    }

                    prev = new KeyValuePair<double, double>(_values[i, 0], _values[i, 1]);
                    first = false;
                }

                var index = _values.Length / _values.Rank;
                x2 = _values[index - 2,0];
                x3 = _values[index - 1,0];
                y2 = _values[index - 2,1];
                y3 = _values[index - 1,1];
                num = (value - x2) / (x3 - x2);
                return y2 + (y3 - y2) * num;
            }
            
        }


        public IEnumerator<KeyValuePair<double, double>> GetEnumerator()
        {
            for (var i = 0; i < _values.Length / _values.Rank; i++)
            {
                yield return new KeyValuePair<double, double>(_values[i, 0], _values[i, 1]);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        
    }