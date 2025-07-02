import JSEncrypt from 'jsencrypt';

export class RsaHelper {
    /**
     * RSA加密
     * @param data 要加密的数据
     * @returns 加密后的数据
     */
    public static encrypt(publicKey: string, data: string): string {

        let encryptor = new JSEncrypt();
        encryptor.setPublicKey(publicKey);
        let result =  encryptor.encrypt(data);
        if(!result){
            throw new Error('加密错误');
        }

        // 后端统一 2048
        return btoa(atob(result));
        // return btoa(atob(result).padStart(2048, "\0"));
    }
}
