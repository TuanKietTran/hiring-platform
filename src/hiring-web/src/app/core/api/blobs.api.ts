import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { BlobObject } from '../models';

const ROOT = '/api/blobs';

@Injectable({ providedIn: 'root' })
export class BlobsApi {
  private readonly http = inject(HttpClient);

  upload(file: File, purpose = 'attachment') {
    const headers = new HttpHeaders({
      'Content-Type': file.type || 'application/octet-stream',
      'X-Blob-Name': file.name,
      'X-Blob-Purpose': purpose,
    });
    return this.http.post<BlobObject>(ROOT, file, { headers });
  }

  delete(id: string) {
    return this.http.delete<void>(`${ROOT}/${id}`);
  }
}
