import SparkMD5 from "spark-md5";

export async function GetFileMd5(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const spark = new SparkMD5.ArrayBuffer();
    const fileReader = new FileReader();

    fileReader.onload = function (event) {
      const result = event.target?.result as ArrayBuffer | undefined;
      if (!result) {
        reject(new Error('FileReader result is null or invalid.'));
        return;
      }
      spark.append(result);
      const md5Hash = spark.end();
      resolve(md5Hash);
    };

    fileReader.onerror = function () {
      reject(new Error('Failed to read file.'));
    };

    fileReader.readAsArrayBuffer(file);
  });
}